using System;

namespace Backend.Application.DTOs.TagTypes;

public class TagTypeDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
}
