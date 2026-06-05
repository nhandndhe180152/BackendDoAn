using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class BlogPost : EntityAuditBase<int>
{
    public int BlogPostCategoryId { get; set; }
    public int BlogPostLayoutId { get; set; }
    public int AuthorId { get; set; }
    public int? CoverImageId { get; set; }
    public string Title { get; set; } = null!;
    public string SeoAlias { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int BlogPostStatusId { get; set; }
    public bool IsApproved { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public DateTime? PublicationDate { get; set; }
    public bool IsShowOnHomePage { get; set; }
    public bool IsPopular { get; set; }
    public virtual BlogPostCategory BlogCategory { get; set; } = null!;
    public virtual BlogPostLayout BlogPostLayout { get; set; } = null!;
    public virtual User Author { get; set; } = null!;
    public virtual FileUpload? CoverImage { get; set; }
    public virtual BlogPostStatus BlogPostStatus { get; set; }
    public virtual ICollection<BlogPostTag> BlogTags { get; set; }
    public virtual ICollection<BlogPostComment> BlogComments { get; set; }
}
