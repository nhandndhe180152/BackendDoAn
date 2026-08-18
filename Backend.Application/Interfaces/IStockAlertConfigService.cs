using System;
using Backend.Application.DTOs.StockAlertConfigs;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockAlertConfigService : IServiceBase<int, CreateStockAlertConfigDto, UpdateStockAlertConfigDto, DTParameter>
{
}
