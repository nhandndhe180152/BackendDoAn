using System;

namespace Backend.Application.DTOs.BlogPostStatuses;

public class CreateBlogPostStatusDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
