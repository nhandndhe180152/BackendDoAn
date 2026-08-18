using System;

namespace Backend.Application.DTOs.StockTakeStatuses;

public class StockTakeStatusDetailDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
