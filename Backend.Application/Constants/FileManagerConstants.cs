using System;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.Constants;

public static class FileManagerConstants
{
    public const int MAXIMUM_TOTAL_FILE = 10;
    public const int MAXIMUM_TOTAL_SIZE = 50 * 1024 * 1024;
    public const int MAXIMUM_TOTAL_SIZE_IN_MB = 50;
    public const int MAXIMUM_IMAGE_SIZE = 10 * 1024 * 1024;
    public const int MAXIMUM_IMAGE_SIZE_IN_MB = 10;

    public static readonly Dictionary<string, int> FileUploadCategory = new()
        {
            {
                "PROFILE",1005
            },
            {
                "BLOG_POST",1018
            },

        };

    public static readonly Dictionary<string, string[]> AllowedExtensionsByCategory = new()
        {
            {
                "BLOG_POST",new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".heic",  ".heif"}
            },
            {
                "PROFILE",new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif", ".bmp", ".tiff", ".heic", ".heif" }
            },
        };

    public static bool IsValidFile(IFormFile file, string category)
    {
        var extension = FileHelper.GetFileExtension(file.FileName);
        var listExtensionAllow = AllowedExtensionsByCategory.GetValueOrDefault(category.ToUpper());

        if (listExtensionAllow == null)
            return false;

        return listExtensionAllow.Contains(extension);
    }

    public static readonly HashSet<string> PublicCategory = new HashSet<string>
        {
            "PROFILE",
            "BLOG_POST"
        };
}
