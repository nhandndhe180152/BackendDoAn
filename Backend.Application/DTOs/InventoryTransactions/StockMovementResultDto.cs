using System;
using Backend.Application.DTOs.Inventories;

namespace Backend.Application.DTOs.InventoryTransactions;

public class StockMovementResultDto
{
    public InventoryDto Inventory { get; set; } = new();

    public InventoryTransactionDto Transaction { get; set; } = new();
}
