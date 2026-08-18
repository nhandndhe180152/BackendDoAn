using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Thực thể doanh nghiệp (tenant) — kiến trúc để sẵn cho SaaS đa tenant.
/// MVP chạy 1 doanh nghiệp; tạo bảng + seed 1 bản ghi mặc định (Tuấn Mây).
/// Các cột OrganizationId ở bảng khác để nullable, CHƯA bật Global Query Filter.
/// </summary>
public class Organization : EntityCommonBase<int>
{
    public string Code { get; set; } = null!;
    public string? TaxCode { get; set; }
    public string? Address { get; set; }
    public string? ContactEmail { get; set; }
    public string? ContactPhone { get; set; }
    public int? LogoFileId { get; set; }

    /// <summary>Free/Pro/Enterprise — pha sau</summary>
    public string? SubscriptionPlan { get; set; }

    /// <summary>Ngày hết hạn gói — pha sau</summary>
    public DateTime? SubscriptionExpiry { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public virtual FileUpload? LogoFile { get; set; }
}
