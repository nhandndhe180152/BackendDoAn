using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.QualityInspections;

/// <summary>
/// Input: autosave kết quả kiểm tra cho 1 bao trong một inspection session.
/// InspectorId / InspectedAt lấy từ backend — không tin client.
/// </summary>
public class SaveBagInspectionResultDto
{
    [Required]
    public int BagId { get; set; }

    [Range(0, 100, ErrorMessage = "MoisturePercent phải trong khoảng 0–100.")]
    public decimal? MoisturePercent { get; set; }

    [Range(0, 100, ErrorMessage = "ImpurityPercent phải trong khoảng 0–100.")]
    public decimal? ImpurityPercent { get; set; }

    [MaxLength(50)]
    public string? MoldLevel { get; set; }

    [MaxLength(50)]
    public string? PestLevel { get; set; }

    [MaxLength(50)]
    public string? PackagingStatus { get; set; }

    /// <summary>PASS | ISSUE_DETECTED (xem BagQualityResultConstants)</summary>
    [Required]
    [MaxLength(30)]
    public string QualityResult { get; set; } = null!;

    /// <summary>
    /// Phụ thuộc InspectionType:
    /// Receiving  → ACCEPT_NORMAL | ACCEPT_QUARANTINE | REJECT_RETURN
    /// Storage    → KEEP_STORED   | QUARANTINE
    /// Recheck    → RELEASE       | KEEP_QUARANTINE
    /// Outbound   → RELEASE       | QUARANTINE
    /// </summary>
    [Required]
    [MaxLength(30)]
    public string Disposition { get; set; } = null!;

    [MaxLength(200)]
    public string? Handling { get; set; }

    [MaxLength(1000)]
    public string? Note { get; set; }

    // ── Gán từ backend, không tin giá trị client gửi lên ────────────────────
    /// <remarks>Controller luôn override bằng GetLoggedInUserId() — client không thể tự set.</remarks>
    public int? InspectorId { get; set; }
}
