using System;

namespace Backend.Application.DTOs.StockTransferStatuses;

public class UpdateStockTransferStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? UpdatedBy { get; set; }
}
