using System;

namespace Backend.Domain.Aggregates;

public class UnitOfMeasureAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
