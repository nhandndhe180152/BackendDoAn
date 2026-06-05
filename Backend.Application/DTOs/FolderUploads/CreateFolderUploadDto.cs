using System;

namespace Backend.Application.DTOs.FolderUploads;

public class CreateFolderUploadDto
{
    public string FolderName { get; set; } = null!;
    public int? ParentId { get; set; }
    public int? CreatedBy { get; set; }
}
