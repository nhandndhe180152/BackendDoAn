using System;

namespace Backend.Application.DTOs.PurchaseOrderStatuses;

public class UpdatePurchaseOrderStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? UpdatedBy { get; set; }
}
