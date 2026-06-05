using System;

namespace Backend.Application.DTOs.BlogPostLayouts;

public class UpdateBlogPostLayoutDto : CreateBlogPostLayoutDto
{
    public int Id { get; set; } = 0;
    public int? UpdatedBy { get; set; }
}
