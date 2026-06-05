using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class BlogPostComment : EntityAuditBase<int>
{
    public int BlogPostId { get; set; }
    public int UserId { get; set; }
    public string Content { get; set; } = null!;
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public bool IsApproved { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public virtual BlogPost BlogPost { get; set; } = null!;
    public virtual User User { get; set; } = null!;
}
