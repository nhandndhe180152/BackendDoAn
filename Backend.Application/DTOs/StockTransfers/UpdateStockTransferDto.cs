using System;

namespace Backend.Application.DTOs.StockTransfers;

public class UpdateStockTransferDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public int FromWarehouseId { get; set; }
    public int ToWarehouseId { get; set; }
    public DateTime TransferDate { get; set; }
    public int? AssignedUserId { get; set; }
    public string? Note { get; set; }
    public int? UpdatedBy { get; set; }
}
