using System;

namespace Backend.Application.DTOs.TagTypes;

public class CreateTagTypeDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
