using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IPaymentMethodRepository : IRepositoryBase<PaymentMethod, int>
{
    Task<DTResult<PaymentMethodAggregate>> GetPagedAsync(DTParameter parameters);
}
