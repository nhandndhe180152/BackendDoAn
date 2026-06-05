using System;

namespace Backend.Application.DTOs.BlogPostStatuses;

public class UpdateBlogPostStatusDto : CreateBlogPostStatusDto
{
    public int Id { get; set; } = 0;
    public int? UpdatedBy { get; set; }
}
