using System;

namespace Backend.Domain.Aggregates;

public class StockTransferStatusAggregate
{
    public int Id { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
