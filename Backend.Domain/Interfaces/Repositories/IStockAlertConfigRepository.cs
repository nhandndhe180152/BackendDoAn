using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IStockAlertConfigRepository : IRepositoryBase<StockAlertConfig, int>
{
    Task<DTResult<StockAlertConfigAggregate>> GetPagedAsync(DTParameter parameters);
}
