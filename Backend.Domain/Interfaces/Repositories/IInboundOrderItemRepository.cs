using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IInboundOrderItemRepository : IRepositoryBase<InboundOrderItem, int>
{
    Task<InboundOrderItem?> GetByIdForWeightAttachAsync(int id);
}
