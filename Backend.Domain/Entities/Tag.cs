using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Tag : EntityCommonBase<int>
{
    public int TagTypeId { get; set; }
    public virtual ICollection<BlogPostTag> BlogTags { get; set; }
    public virtual TagType TagType { get; set; }
}