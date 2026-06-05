using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class TagType : EntityCommonBase<int>
{
    public virtual ICollection<Tag> Tags { get; set; }
}
