using System;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IReturnToSupplierOrderStatusService : IServiceBase<int, CreateReturnToSupplierOrderStatusDto, UpdateReturnToSupplierOrderStatusDto, DTParameter>
{
}
