using System;
using Backend.Domain.Abstractions.Entities;

namespace Backend.Domain.Abstractions;

public abstract class EntityBase<TKey> : IEntityBase<TKey>
{
    public TKey Id { get; set; }
    public bool IsDeleted { get; set; }
}
