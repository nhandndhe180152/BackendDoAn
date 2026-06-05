using System;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.Interfaces;

public interface IStorageService
{
    string GetTemporaryUrl(string key);

    string GetImageUrl(string fileName);
    string GetOriginalUrl(string key);
    string GetPublicUrl(string filePath);
    Task<FileUploadResult> UploadAsync(IFormFile file, string? folder = null);
    Task<List<FileUploadResult>> UploadMultipleAsync(List<IFormFile> files, string? folder = null);
    Task<FolderUploadResult> CreateFolderAsync(CreateFolderRequest request);
    bool ValidateTemporaryAccess(string path, long expires, string token);
    Task<Stream> DownloadAsync(string filePath);
    Task DeleteAsync(string filePath);
    Task<string> UploadCloudAsync(IFormFile file, string category);

    Task<bool> DeleteCloudAsync(string fileUrl);
}
