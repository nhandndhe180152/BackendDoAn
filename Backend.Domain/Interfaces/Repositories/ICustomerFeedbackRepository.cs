using Backend.Domain.Entities;
using Backend.Domain.Abstractions.Repositories;

namespace Backend.Domain.Interfaces.Repositories;

public interface ICustomerFeedbackRepository : IRepositoryBase<CustomerFeedback, int>
{
}
