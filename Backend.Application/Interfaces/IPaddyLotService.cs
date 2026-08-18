using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyLots;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyLotService : IServiceBase<int, CreatePaddyLotDto, UpdatePaddyLotDto, DTParameter>
{
    /// <summary>Danh sách lô đang CHỜ KIỂM ĐỊNH (status = AWAITING_QC) cho màn Chất lượng &amp; cách ly.</summary>
    Task<ApiResponse> GetAwaitingQualityInspectionAsync();

    /// <summary>Danh sách lô đang CÁCH LY (status = QUARANTINE) — nguồn cho ô chọn lô khi KIỂM TRA LẠI chất lượng.</summary>
    Task<ApiResponse> GetQuarantinedAsync();
}
