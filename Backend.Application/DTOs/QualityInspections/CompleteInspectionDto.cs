using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.QualityInspections;

/// <summary>
/// Input cho POST /api/v1/quality-inspections/{inspectionId}/complete.
/// Body nhỏ gọn — không gửi lại bag results.
/// </summary>
public class CompleteInspectionDto
{
    [MaxLength(1000)]
    public string? Note { get; set; }

    // ── Gán từ backend ────────────────────────────────────────────────────────
    public int? CompletedBy { get; set; }
}
