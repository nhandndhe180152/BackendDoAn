using System;
using Backend.Domain.Abstractions.Entities;

namespace Backend.Domain.Abstractions;

public abstract class EntityCommonBase<Tkey> : EntityAuditBase<Tkey>, IEntityCommonBase
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}
