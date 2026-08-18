using System;

namespace Backend.Application.DTOs.PaddyPurchaseSchedules;

public class PaddyPurchaseScheduleDetailDto
{
    public int Id { get; set; }
    public int? OrganizationId { get; set; }
    public string ScheduleCode { get; set; } = null!;
    public int FarmerId { get; set; }
    public string? FarmerName { get; set; }
    public int StatusId { get; set; }
    public string? StatusName { get; set; }
    public string? StatusCode { get; set; }
    public int? RiceVarietyId { get; set; }
    public string? RiceVarietyName { get; set; }
    public DateTime ScheduleDate { get; set; }
    public string? Location { get; set; }
    public decimal? EstimatedQtyKg { get; set; }
    public decimal? ExpectedPrice { get; set; }
    public int? AssignedUserId { get; set; }
    public int? WarehouseId { get; set; }
    public string? WarehouseName { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }

    // ── Tình trạng lập phiếu mua từ lịch (dùng để chặn tạo phiếu trùng) ─────
    /// <summary>Số phiếu mua lúa chưa xóa thuộc lịch này.</summary>
    public int ReceiptCount { get; set; }
    /// <summary>Tổng khối lượng thực tế (kg) của các phiếu mua lúa chưa xóa thuộc lịch này.</summary>
    public decimal ReceiptedWeightKg { get; set; }
    /// <summary>Khối lượng còn lại có thể lập phiếu (null khi lịch không khai báo khối lượng dự kiến).</summary>
    public decimal? RemainingQtyKg { get; set; }
    /// <summary>Lịch còn được lập thêm phiếu mua lúa hay không.</summary>
    public bool CanCreateReceipt { get; set; }
}
