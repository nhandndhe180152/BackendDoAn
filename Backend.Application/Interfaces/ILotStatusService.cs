using System;
using Backend.Application.DTOs.LotStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ILotStatusService : IServiceBase<int, CreateLotStatusDto, UpdateLotStatusDto, DTParameter>
{
}
