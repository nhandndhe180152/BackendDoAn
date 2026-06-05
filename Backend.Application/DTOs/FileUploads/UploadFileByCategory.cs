using System;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.DTOs.FileUploads;

public class UploadFileByCategory
{
    public List<IFormFile> Files { get; set; } = new List<IFormFile>();
    public int? CreatedBy { get; set; }
    public string Category { get; set; } = null!;
    public int? DirectionId { get; set; }
    public bool IsExtractText { get; set; } = false;
    public bool IsAddNew { get; set; } = false;
}
