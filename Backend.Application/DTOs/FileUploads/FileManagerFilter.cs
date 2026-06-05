using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.FileUploads;

public class FileManagerFilter : SearchQuery
{
    public List<string> FileTypes { get; set; } = new List<string>();
    public int? DirectionId { get; set; }
    public bool IsGetAll { get; set; }
}
