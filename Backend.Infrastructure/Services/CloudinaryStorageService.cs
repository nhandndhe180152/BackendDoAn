using System;
using Backend.Application.Interfaces;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Infrastructure.Services;

public class CloudinaryStorageService : IStorageService
{
    private readonly Cloudinary _cloudinary;
    private readonly ILogger<CloudinaryStorageService> _logger;

    public CloudinaryStorageService(IOptions<CloudinarySettings> config, ILoggerFactory loggerFactory)
    {
        var acc = new Account(config.Value.CloudName, config.Value.ApiKey, config.Value.ApiSecret);
        _cloudinary = new Cloudinary(acc);
        _logger = loggerFactory.CreateLogger<CloudinaryStorageService>();
    }

    // === Upload một file lên Cloudinary, FilePath trả về = Cloudinary URL ===
    // FileUploadService lưu FilePath này vào FileUpload.FileKey trong DB
    public async Task<FileUploadResult> UploadAsync(IFormFile file, string? folder = null)
    {
        try
        {
            using var stream = file.OpenReadStream();

            UploadResult uploadResult;

            if (FileHelper.IsImage(file))
            {
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"vcar/{folder ?? "general"}",
                    PublicId = Guid.NewGuid().ToString(),
                    Transformation = new Transformation()
                        .Width(1600).Crop("limit")
                        .FetchFormat("webp").Quality("auto")
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }
            else
            {
                var uploadParams = new RawUploadParams
                {
                    File = new FileDescription(file.FileName, stream),
                    Folder = $"vcar/{folder ?? "general"}",
                    PublicId = Guid.NewGuid().ToString()
                };
                uploadResult = await _cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult.Error != null)
            {
                _logger.LogError("Cloudinary upload error for {FileName}: {Error}", file.FileName, uploadResult.Error.Message);
                return new FileUploadResult { Success = false, ErrorMessage = uploadResult.Error.Message };
            }

            return new FileUploadResult
            {
                Success = true,
                FileName = file.FileName,
                FilePath = uploadResult.SecureUrl.ToString(), // Cloudinary URL lưu vào FileKey
                FileSize = uploadResult.Bytes
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed for file: {FileName}", file.FileName);
            return new FileUploadResult { Success = false, ErrorMessage = "Upload failed: " + ex.Message };
        }
    }

    // === Upload nhiều file song song ===
    public async Task<List<FileUploadResult>> UploadMultipleAsync(List<IFormFile> files, string? folder = null)
    {
        var tasks = files.Select(f => UploadAsync(f, folder));
        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    // === UploadCloudAsync: gọi nội bộ UploadAsync (tương thích method cũ) ===
    public async Task<string> UploadCloudAsync(IFormFile file, string category)
    {
        var result = await UploadAsync(file, category);
        return result.Success ? result.FilePath : null;
    }

    // === GetOriginalUrl: FileKey là Cloudinary URL đầy đủ, trả về chính nó ===
    public string GetOriginalUrl(string key) => key ?? string.Empty;

    // === GetTemporaryUrl: Cloudinary URL đã public, trả về như GetOriginalUrl ===
    // Nếu muốn ảnh private, có thể generate Cloudinary signed URL ở đây
    public string GetTemporaryUrl(string key) => key ?? string.Empty;

    // === GetPublicUrl/GetImageUrl: tương tự ===
    public string GetPublicUrl(string filePath) => filePath ?? string.Empty;
    public string GetImageUrl(string fileName) => fileName ?? string.Empty;

    // === CreateFolderAsync: Cloudinary không cần tạo folder vật lý ===
    // Folder (prefix) sẽ tự tạo khi có file upload vào. Chỉ cần trả về success
    // để FileUploadService tiếp tục lưu FolderUpload vào DB như bình thường.
    public Task<FolderUploadResult> CreateFolderAsync(CreateFolderRequest request)
    {
        return Task.FromResult(new FolderUploadResult
        {
            Success = true,
            FolderPath = request.FolderPath
        });
    }

    // === DeleteAsync/DeleteCloudAsync: xóa ảnh trên Cloudinary ===
    public async Task DeleteAsync(string filePath)
    {
        await DeleteCloudAsync(filePath);
    }

    public async Task<bool> DeleteCloudAsync(string fileUrl)
    {
        if (string.IsNullOrEmpty(fileUrl)) return false;
        try
        {
            var publicId = ExtractPublicIdFromUrl(fileUrl);
            if (string.IsNullOrEmpty(publicId)) return false;

            var deletionParams = new DeletionParams(publicId) { ResourceType = ResourceType.Image };
            var result = await _cloudinary.DestroyAsync(deletionParams);
            return result.Result == "ok";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete failed for URL: {FileUrl}", fileUrl);
            return false;
        }
    }

    // === ValidateTemporaryAccess: không áp dụng cho Cloudinary ===
    public bool ValidateTemporaryAccess(string path, long expires, string token) => true;

    // === DownloadAsync: download từ Cloudinary URL về MemoryStream ===
    public async Task<Stream> DownloadAsync(string filePath)
    {
        using var httpClient = new System.Net.Http.HttpClient();
        var response = await httpClient.GetAsync(filePath);
        response.EnsureSuccessStatusCode();
        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    // === Helper: tách PublicId từ Cloudinary URL để xóa ===
    private string ExtractPublicIdFromUrl(string url)
    {
        try
        {
            var uri = new Uri(url);
            var segments = uri.AbsolutePath.Split('/');
            int uploadIndex = Array.IndexOf(segments, "upload");
            if (uploadIndex == -1) return null;

            int startIndex = uploadIndex + 1;
            if (startIndex < segments.Length
                && segments[startIndex].StartsWith("v")
                && segments[startIndex].Length > 1
                && char.IsDigit(segments[startIndex][1]))
            {
                startIndex++;
            }

            var pathWithExtension = string.Join("/", segments.Skip(startIndex));
            return Path.ChangeExtension(pathWithExtension, null);
        }
        catch { return null; }
    }
}
