using System;
using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

public class StockTakeDto
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public int StockTakeStatusId { get; set; }
    public string STCode { get; set; } = null!;
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public DateTime CreatedDate { get; set; }
    
    public List<StockTakeItemDto> StockTakeItems { get; set; } = new List<StockTakeItemDto>();
}

public class CreateStockTakeDto
{
    public int WarehouseId { get; set; }
    public int StockTakeStatusId { get; set; } = (int)Backend.Domain.Enums.Enums.StockTakeStatusEnum.Draft;
    public string? STCode { get; set; } // Auto-generated if null
    public string? Note { get; set; }
    public int? CreatedBy { get; set; }
    
    public List<CreateStockTakeItemDto> StockTakeItems { get; set; } = new List<CreateStockTakeItemDto>();
}

public class UpdateStockTakeDto
{
    public int Id { get; set; }
    public int StockTakeStatusId { get; set; }
    public string? Note { get; set; }
    public DateTime? StartedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public int? ApprovedByUserId { get; set; }
    public int? UpdatedBy { get; set; }
    
    public List<UpdateStockTakeItemDto> StockTakeItems { get; set; } = new List<UpdateStockTakeItemDto>();
}
