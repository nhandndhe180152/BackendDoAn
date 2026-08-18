using System.Collections.Generic;

namespace Backend.Application.DTOs.QualityInspections;

/// <summary>
/// Response của GET /api/v1/quality-inspections/{inspectionId}/bags.
/// Tổng hợp tiến độ kiểm tra bag-level của một inspection session.
/// </summary>
public class QualityInspectionBagProgressDto
{
    public int InspectionId { get; set; }

    /// <summary>RECEIVING | STORAGE | RECHECK | OUTBOUND_EXCEPTION | null (legacy)</summary>
    public string? InspectionType { get; set; }

    public int LotId { get; set; }
    public string? LotCode { get; set; }

    public bool IsCompleted { get; set; }

    // ── Thống kê tiến độ ─────────────────────────────────────────────────────
    public int TotalBags { get; set; }
    public int InspectedBags { get; set; }
    public int RemainingBags { get; set; }

    // ── Thống kê kết quả ─────────────────────────────────────────────────────
    /// <summary>Số bao kết quả ACCEPT_NORMAL hoặc KEEP_STORED.</summary>
    public int NormalBags { get; set; }

    /// <summary>Số bao kết quả ACCEPT_QUARANTINE hoặc QUARANTINE.</summary>
    public int QuarantineBags { get; set; }

    /// <summary>Số bao kết quả REJECT_RETURN.</summary>
    public int RejectedBags { get; set; }

    /// <summary>Số bao kết quả RELEASE (Recheck).</summary>
    public int ReleasedBags { get; set; }

    // ── Danh sách bag items ───────────────────────────────────────────────────
    public List<QualityInspectionBagResultDto> Items { get; set; } = new();
}
