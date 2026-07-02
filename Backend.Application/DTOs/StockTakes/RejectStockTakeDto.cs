using System;

namespace Backend.Application.DTOs.StockTakes;

public class RejectStockTakeDto
{
    public string Reason { get; set; } = null!;
}
