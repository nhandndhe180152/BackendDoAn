using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class BlogPostCategory : EntityCommonBase<int>
{
    public string Color { get; set; } = null!;
    public string SeoAlias { get; set; } = null!;
    public virtual ICollection<BlogPost> BlogPosts { get; set; }
}
