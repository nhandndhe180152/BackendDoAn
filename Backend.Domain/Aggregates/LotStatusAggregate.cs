using System;

namespace Backend.Domain.Aggregates;

public class LotStatusAggregate
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool IsSellable { get; set; }
    public DateTime CreatedDate { get; set; }
}
