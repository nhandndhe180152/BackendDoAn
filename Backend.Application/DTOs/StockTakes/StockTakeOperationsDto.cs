using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

public class SaveStockTakeCountsDto
{
    public string? Note { get; set; }
    public List<SaveStockTakeCountItemDto> Items { get; set; } = new();
}

public class SaveStockTakeCountItemDto
{
    public int Id { get; set; }
    public decimal? ActualQuantity { get; set; }
    public string? Note { get; set; }
    public bool QRScanned { get; set; }
    public bool RecountConfirmed { get; set; }
}

public class SubmitStockTakeDto
{
    public string? Note { get; set; }
}
