using System;

namespace Backend.Application.DTOs.StockTransferStatuses;

public class StockTransferStatusDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
