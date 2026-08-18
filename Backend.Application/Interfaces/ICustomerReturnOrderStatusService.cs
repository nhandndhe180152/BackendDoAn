using System;
using Backend.Application.DTOs.CustomerReturnOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ICustomerReturnOrderStatusService : IServiceBase<int, CreateCustomerReturnOrderStatusDto, UpdateCustomerReturnOrderStatusDto, DTParameter>
{
}
