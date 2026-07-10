using System.Collections.Generic;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Danh mục khách hàng — tách thông tin khách khỏi OutboundOrder.
/// Cấu trúc đối xứng với Supplier/Farmer.
/// </summary>
public class Customer : EntityAuditBase<int>
{
    public int? OrganizationId { get; set; }
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;

    /// <summary>Lẻ / Nhà buôn</summary>
    public string? CustomerType { get; set; }

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? TaxCode { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual Organization? Organization { get; set; }
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
}
