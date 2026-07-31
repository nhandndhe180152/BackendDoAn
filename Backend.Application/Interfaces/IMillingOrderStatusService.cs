using System;
using Backend.Application.DTOs.MillingOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IMillingOrderStatusService : IServiceBase<int, CreateMillingOrderStatusDto, UpdateMillingOrderStatusDto, DTParameter>
{
}
