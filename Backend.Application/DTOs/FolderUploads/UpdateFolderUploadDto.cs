using System;

namespace Backend.Application.DTOs.FolderUploads;

public class UpdateFolderUploadDto : CreateFolderUploadDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
