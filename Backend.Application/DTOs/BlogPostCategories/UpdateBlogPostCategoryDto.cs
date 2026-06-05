using System;

namespace Backend.Application.DTOs.BlogPostCategories;

public class UpdateBlogPostCategoryDto : CreateBlogPostCategoryDto
{
    public int Id { get; set; } = 0;
    public int? UpdatedBy { get; set; }
}
