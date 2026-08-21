using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;

namespace Backend.Infrastructure.Repositories;

public class CustomerFeedbackRepository : RepositoryBase<CustomerFeedback, int>, ICustomerFeedbackRepository
{
    public CustomerFeedbackRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
    }
}
