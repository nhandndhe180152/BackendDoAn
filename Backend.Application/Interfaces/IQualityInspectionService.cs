using System.Threading.Tasks;
using Backend.Application.DTOs.QualityInspections;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IQualityInspectionService : IServiceBase<int, CreateQualityInspectionDto, UpdateQualityInspectionDto, DTParameter>
{
    Task<ApiResponse> GetByLotAsync(int paddyLotId);
}
