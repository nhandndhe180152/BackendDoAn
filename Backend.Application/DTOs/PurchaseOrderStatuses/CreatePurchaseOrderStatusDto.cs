using System;

namespace Backend.Application.DTOs.PurchaseOrderStatuses;

public class CreatePurchaseOrderStatusDto
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
