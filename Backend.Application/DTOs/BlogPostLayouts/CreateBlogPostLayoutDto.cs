using System;

namespace Backend.Application.DTOs.BlogPostLayouts;

public class CreateBlogPostLayoutDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
