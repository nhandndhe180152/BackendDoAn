using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class BlogPostTag : EntityAuditBase<int>
{
    public int BlogPostId { get; set; }
    public int TagId { get; set; }
    public virtual BlogPost BlogPost { get; set; }
    public virtual Tag Tag { get; set; }
}
