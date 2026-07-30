using System;
using Backend.Application.DTOs.SalesOrderStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISalesOrderStatusService : IServiceBase<int, CreateSalesOrderStatusDto, UpdateSalesOrderStatusDto, DTParameter>
{
}
