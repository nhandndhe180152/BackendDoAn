using System.Threading.Tasks;
using Backend.Application.DTOs.QualityInspections;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IQualityInspectionService : IServiceBase<int, CreateQualityInspectionDto, UpdateQualityInspectionDto, DTParameter>
{
    Task<ApiResponse> GetByLotAsync(int paddyLotId);

    /// <summary>Kiểm tra lại chất lượng lô đang CÁCH LY; nếu đạt sẽ rút hàng khỏi ô cách ly và tạo phiếu nhập kho để xếp lại.</summary>
    Task<ApiResponse> RecheckAsync(CreateQualityInspectionDto obj);

    // ── W14-D: Bag-level APIs ─────────────────────────────────────────────────

    /// <summary>
    /// GET tiến độ kiểm tra bag-level của một inspection session.
    /// Trả QualityInspectionBagProgressDto với danh sách bag và counters.
    /// </summary>
    Task<ApiResponse> GetBagProgressAsync(int inspectionId);

    /// <summary>
    /// PUT autosave kết quả kiểm tra cho 1 bao (upsert idempotent).
    /// Chỉ lưu QC result — không có side-effect inventory/lot/debt.
    /// </summary>
    Task<ApiResponse> SaveBagResultAsync(int inspectionId, SaveBagInspectionResultDto dto);

    // ── W14-E ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Hoàn tất inspection session (POST /complete).
    /// Validate 100% bag đã kiểm tra → finalize lot/bag theo InspectionType
    /// → aggregate header → mark CompletedAt.
    /// Idempotent: nếu đã Complete trả 409.
    /// </summary>
    Task<ApiResponse> CompleteAsync(int inspectionId, CompleteInspectionDto dto);

    // ── W14-J: Moisture Config ────────────────────────────────────────────────────
    /// <summary>
    /// Trả về cấu hình ngưỡng độ ẩm (ReceivingQcMoistureMinPercent và ReceivingQcMoistureMaxPercent)
    /// được lưu trong SystemConfig. Frontend sử dụng để hiển thị cảnh báo, không hard-code.
    /// </summary>
    Task<ApiResponse> GetMoistureConfigAsync();
}

