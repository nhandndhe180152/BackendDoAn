using System;

namespace Backend.Application.DTOs.FolderUploads;

public class FolderUploadListDto
{
    public int Id { get; set; }
    public string FolderName { get; set; } = null!;
    public string FolderPath { get; set; } = null!;
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
    public List<FolderUploadListDto> Childs { get; set; } = new List<FolderUploadListDto>();
}
