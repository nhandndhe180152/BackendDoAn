using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái đơn trả hàng về nhà cung cấp (FE-16)
/// </summary>
public class ReturnToSupplierOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Color { get; set; } = null!;
}
