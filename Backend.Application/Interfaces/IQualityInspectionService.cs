using System.Threading.Tasks;
using Backend.Application.DTOs.QualityInspections;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IQualityInspectionService : IServiceBase<int, CreateQualityInspectionDto, UpdateQualityInspectionDto, DTParameter>
{
    Task<ApiResponse> GetByLotAsync(int paddyLotId);

    /// <summary>Kiểm tra lại chất lượng lô đang CÁCH LY; nếu đạt sẽ rút hàng khỏi ô cách ly và tạo phiếu nhập kho để xếp lại.</summary>
    Task<ApiResponse> RecheckAsync(CreateQualityInspectionDto obj);
}
