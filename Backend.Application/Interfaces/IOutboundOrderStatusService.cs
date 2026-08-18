using System;
using Backend.Application.DTOs.OutboundOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IOutboundOrderStatusService : IServiceBase<int, CreateOutboundOrderStatusDto, UpdateOutboundOrderStatusDto, DTParameter>
{
}
