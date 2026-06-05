using System;

namespace Backend.Domain.Abstractions.Entities;

public interface IEntityBase<TKey>
{
    public TKey Id { get; set; }
    public bool IsDeleted { get; set; }
}
