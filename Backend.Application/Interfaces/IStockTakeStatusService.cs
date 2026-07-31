using System;
using Backend.Application.DTOs.StockTakeStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTakeStatusService : IServiceBase<int, CreateStockTakeStatusDto, UpdateStockTakeStatusDto, DTParameter>
{
}
