using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyPurchaseScheduleService : IServiceBase<int, CreatePaddyPurchaseScheduleDto, UpdatePaddyPurchaseScheduleDto, DTParameter>
{
    Task<ApiResponse> UpdateStatusAsync(int id, int statusId, int updatedBy);
}
