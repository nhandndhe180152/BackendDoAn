using System;
using Backend.Application.DTOs.StockTransferStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IStockTransferStatusService : IServiceBase<int, CreateStockTransferStatusDto, UpdateStockTransferStatusDto, DTParameter>
{
}
