using System;

namespace Backend.Application.DTOs.BlogPosts;

public class CreateBlogPostDto
{
    public int BlogPostCategoryId { get; set; }
    public int BlogPostLayoutId { get; set; }
    public int? CoverImageId { get; set; }
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int BlogPostStatusId { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? PublicationDate { get; set; }
    public bool IsShowOnHomePage { get; set; }
    public bool IsPopular { get; set; }
    public int? CreatedBy { get; set; }
    public List<int> BlogPostTagIds { get; set; } = new List<int>();
}
