using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class UnitOfMeasure : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
}
