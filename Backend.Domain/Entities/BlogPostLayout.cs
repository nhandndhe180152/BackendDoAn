using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class BlogPostLayout : EntityCommonBase<int>
{
    public virtual ICollection<BlogPost> BlogPosts { get; set; }
}
