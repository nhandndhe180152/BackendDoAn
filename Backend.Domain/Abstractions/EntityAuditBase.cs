using System;
using Backend.Domain.Abstractions.Entities;

namespace Backend.Domain.Abstractions;

public abstract class EntityAuditBase<Tkey> : EntityBase<Tkey>, IAuditable
{
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
