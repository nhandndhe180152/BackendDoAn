using System;
using Backend.Application.DTOs.FileUploads;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.BlogPosts;

public class BlogPostDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string SeoAlias { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Content { get; set; } = null!;
    public bool IsApproved { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? PublicationDate { get; set; }
    public bool IsShowOnHomePage { get; set; }
    public bool IsPopular { get; set; }
    public virtual FileUploadDetailDto? CoverImage { get; set; }
    public DataItem<int> BlogCategory { get; set; } = new DataItem<int>();
    public DataItem<int> BlogPostLayout { get; set; } = new DataItem<int>();
    public DataItem<int> Author { get; set; } = new DataItem<int>();
    public DataItem<int> BlogPostStatus { get; set; } = new DataItem<int>();
    public List<DataItem<int>> BlogPostTagIds { get; set; } = new List<DataItem<int>>();
}