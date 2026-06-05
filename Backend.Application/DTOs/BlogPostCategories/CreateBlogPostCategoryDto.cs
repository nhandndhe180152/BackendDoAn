using System;

namespace Backend.Application.DTOs.BlogPostCategories;

public class CreateBlogPostCategoryDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
