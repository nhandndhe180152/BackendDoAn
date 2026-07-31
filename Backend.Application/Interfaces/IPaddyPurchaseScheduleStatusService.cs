using System;
using Backend.Application.DTOs.PaddyPurchaseScheduleStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaddyPurchaseScheduleStatusService : IServiceBase<int, CreatePaddyPurchaseScheduleStatusDto, UpdatePaddyPurchaseScheduleStatusDto, DTParameter>
{
}
