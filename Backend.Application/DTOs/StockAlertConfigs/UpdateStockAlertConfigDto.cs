using System;

namespace Backend.Application.DTOs.StockAlertConfigs;

public class UpdateStockAlertConfigDto : CreateStockAlertConfigDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
