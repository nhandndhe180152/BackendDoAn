using System;

namespace Backend.Application.DTOs.Tags;

public class CreateTagDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
    public int TagTypeId { get; set; }
}
