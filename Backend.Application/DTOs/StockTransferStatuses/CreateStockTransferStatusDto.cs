using System;

namespace Backend.Application.DTOs.StockTransferStatuses;

public class CreateStockTransferStatusDto
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
