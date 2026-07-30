using System;

namespace Backend.Domain.Aggregates;

public class OutboundOrderStatusAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
