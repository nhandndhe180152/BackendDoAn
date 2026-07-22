using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Trạng thái đơn hoàn hàng từ khách (FE-16)
/// </summary>
public class CustomerReturnOrderStatus : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Color { get; set; } = null!;
}
