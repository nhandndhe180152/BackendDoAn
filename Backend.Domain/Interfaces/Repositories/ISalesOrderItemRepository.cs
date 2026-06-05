using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ISalesOrderItemRepository : IRepositoryBase<SalesOrderItem, int>
{
    Task<SalesOrderItem?> GetByIdForWeightAttachAsync(int id);
}
