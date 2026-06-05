using System;

namespace Backend.Application.DTOs.BlogPostLayouts;

public class BlogPostLayoutDetailDto
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
}
