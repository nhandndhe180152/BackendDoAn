using System;
using Microsoft.AspNetCore.Http;

namespace Backend.Share.Helpers;

/// <summary>
/// Helper validate file upload, phân loại file theo extension/MIME type và kiểm tra tên thư mục hợp lệ.
/// </summary>
public static class FileHelper
{

    private static readonly Dictionary<string, string[]> AllowedExtensionsByMimeType = new()
        {
            // Images
            { "image/jpeg", new[] { ".jpg", ".jpeg" } },
            { "image/png", new[] { ".png" } },
            { "image/webp", new[] { ".webp" } },
            { "image/gif", new[] { ".gif" } },
            { "image/bmp", new[] { ".bmp" } },
            { "image/tiff", new[] { ".tiff" } },
            { "image/heic", new[] { ".heic" } },
            { "image/heif", new[] { ".heif" } },

            // Documents
            { "application/pdf", new[] { ".pdf" } },
            { "application/msword", new[] { ".doc" } },
            { "application/vnd.openxmlformats-officedocument.wordprocessingml.document", new[] { ".docx" } },
            { "application/vnd.ms-excel", new[] { ".xls" } },
            { "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", new[] { ".xlsx" } },
            { "application/vnd.ms-excel.sheet.macroEnabled.12", new[] { ".xlsm" } },
            { "application/vnd.ms-excel.sheet.binary.macroEnabled.12", new[] { ".xlsb" } },
            { "application/vnd.ms-powerpoint", new[] { ".ppt" } },
            { "application/vnd.openxmlformats-officedocument.presentationml.presentation", new[] { ".pptx" } },
            { "text/plain", new[] { ".txt" } },

            // Audio
            { "audio/mpeg", new[] { ".mp3" } },
            { "audio/wav", new[] { ".wav" } },
            { "audio/ogg", new[] { ".ogg" } },

            // Video
            { "video/mp4", new[] { ".mp4" } },
            { "video/webm", new[] { ".webm" } },
            { "video/quicktime", new[] { ".mov" } },
            { "video/x-msvideo", new[] { ".avi" } },
        };

    private static readonly HashSet<string> ImageExtensions = new() { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".heic", ".heif" };
    private static readonly HashSet<string> DocumentExtensions = new() { ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".xlsm", ".xlsb", ".ppt", ".pptx", ".txt" };
    private static readonly HashSet<string> AudioExtensions = new() { ".mp3", ".wav", ".ogg" };
    private static readonly HashSet<string> VideoExtensions = new() { ".mp4", ".webm", ".mov", ".avi" };
    /// <summary>
    /// Lấy phần mở rộng file và chuyển về chữ thường để so sánh thống nhất với danh sách cho phép.
    /// </summary>
    /// <param name="fileName">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string GetFileExtension(string fileName)
    {
        return Path.GetExtension(fileName).ToLowerInvariant();
    }

    /// <summary>
    /// Tìm MIME type tương ứng với extension dựa trên bảng allowed mapping.
    /// </summary>
    /// <param name="extension">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string? GetMimeType(string extension)
    {
        extension = extension.ToLower();
        return AllowedExtensionsByMimeType.FirstOrDefault(kvp => kvp.Value.Contains(extension)).Key;
    }

    /// <summary>
    /// Lấy danh sách extension hợp lệ cho một MIME type cụ thể.
    /// </summary>
    /// <param name="mimeType">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static string[]? GetExtensions(string mimeType)
    {
        return AllowedExtensionsByMimeType.TryGetValue(mimeType, out var extensions) ? extensions : null;
    }

    /// <summary>
    /// Validate file upload theo ba bước: file không rỗng, không vượt quá dung lượng tối đa, và MIME type phải khớp extension được phép.
    /// </summary>
    /// <param name="file">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="maxSizeInBytes">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <param name="error">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsValidFile(IFormFile file, long maxSizeInBytes, out string error)
    {
        error = string.Empty;

        if (file == null || file.Length == 0)
        {
            error = "File is empty.";
            return false;
        }

        if (file.Length > maxSizeInBytes)
        {
            error = $"File size exceeds the limit of {maxSizeInBytes / 1024 / 1024} MB.";
            return false;
        }

        var extension = GetFileExtension(file.FileName).ToLowerInvariant();
        var mimeType = file.ContentType;

        if (!AllowedExtensionsByMimeType.TryGetValue(mimeType, out var validExtensions) ||
            !validExtensions.Contains(extension))
        {
            error = $"Invalid file type: {mimeType} ({extension})";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Trả về toàn bộ extension hệ thống cho phép upload, đã distinct và sắp xếp để dùng cho UI hoặc message validation.
    /// </summary>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static IEnumerable<string> GetAllowedExtensions()
    {
        return AllowedExtensionsByMimeType.Values.SelectMany(e => e).Distinct().OrderBy(e => e);
    }

    /// <summary>
    /// Kiểm tra file hoặc extension có thuộc nhóm ảnh được phép hay không.
    /// </summary>
    /// <param name="file">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsImage(this IFormFile file)
    {
        var ext = GetFileExtension(file.FileName).ToLowerInvariant();
        return ImageExtensions.Contains(ext);
    }

    /// <summary>
    /// Kiểm tra file hoặc extension có thuộc nhóm ảnh được phép hay không.
    /// </summary>
    /// <param name="ext">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsImage(string ext)
    {
        return ImageExtensions.Contains(ext);
    }

    public static bool IsDocument(this IFormFile file)
    {
        var ext = GetFileExtension(file.FileName).ToLowerInvariant();
        return DocumentExtensions.Contains(ext);
    }
    public static bool IsDocument(string ext)
    {
        return DocumentExtensions.Contains(ext);
    }

    public static bool IsAudio(this IFormFile file)
    {
        var ext = GetFileExtension(file.FileName).ToLowerInvariant();
        return AudioExtensions.Contains(ext);
    }
    public static bool IsAudio(string ext)
    {
        return AudioExtensions.Contains(ext);
    }

    public static bool IsVideo(this IFormFile file)
    {
        var ext = GetFileExtension(file.FileName).ToLowerInvariant();
        return VideoExtensions.Contains(ext);
    }
    public static bool IsVideo(string ext)
    {
        return VideoExtensions.Contains(ext);
    }

    /// <summary>
    /// Kiểm tra tên folder không chứa ký tự cấm của hệ điều hành, tránh lỗi khi tạo thư mục lưu file.
    /// </summary>
    /// <param name="folderName">Tham số đầu vào dùng trong logic xử lý của hàm.</param>
    /// <returns>Kết quả xử lý của hàm, thường là dữ liệu, ApiResponse, IActionResult hoặc trạng thái thao tác.</returns>
    public static bool IsValidFolderName(string folderName)
    {
        var inValidChars = Path.GetInvalidFileNameChars();
        return inValidChars.All(character => !folderName.Contains(character));
    }
}
