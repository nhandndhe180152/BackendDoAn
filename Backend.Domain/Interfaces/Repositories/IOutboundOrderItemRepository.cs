using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IOutboundOrderItemRepository : IRepositoryBase<OutboundOrderItem, int>
{
    Task<OutboundOrderItem?> GetByIdForWeightAttachAsync(int id);
}
