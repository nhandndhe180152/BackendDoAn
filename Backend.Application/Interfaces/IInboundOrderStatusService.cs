using System;
using Backend.Application.DTOs.InboundOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IInboundOrderStatusService : IServiceBase<int, CreateInboundOrderStatusDto, UpdateInboundOrderStatusDto, DTParameter>
{
}
