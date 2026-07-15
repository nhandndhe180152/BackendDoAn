using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyLots;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyLotService : IServiceBase<int, CreatePaddyLotDto, UpdatePaddyLotDto, DTParameter>
{
}
