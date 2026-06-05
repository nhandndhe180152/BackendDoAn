using System;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.DTOs.FileUploads;

public class UploadFileRequest
{
    public List<IFormFile> Files { get; set; } = new List<IFormFile>();
    public int FolderUploadId { get; set; }
    public int? CreatedBy { get; set; }
}
