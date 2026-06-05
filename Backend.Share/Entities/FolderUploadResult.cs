using System;

namespace Backend.Share.Entities;

public class FolderUploadResult
{
    public bool Success { get; set; }
    //public string FolderName { get; set; } = string.Empty;
    public string FolderPath { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
}
