using System;

namespace Backend.Application.DTOs.FileUploads;

public class FileUploadDetailDto
{
    public int Id { get; set; }
    public string FileName { get; set; } = null!;
    public long FileSize { get; set; }
    public string FileKey { get; set; } = null!;
    public string FileType { get; set; } = null!;
    public string Url { get; set; } = null!;
}
