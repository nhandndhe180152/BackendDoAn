using System;

namespace Backend.Application.DTOs.BlogPosts;

public class UpdateBlogPostDto : CreateBlogPostDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
