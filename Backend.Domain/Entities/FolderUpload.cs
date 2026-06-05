using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class FolderUpload : EntityAuditBase<int>
{
    public string FolderName { get; set; } = null!;
    public string FolderPath { get; set; } = null!;
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public virtual ICollection<FileUpload> FileUploads { get; set; }
}