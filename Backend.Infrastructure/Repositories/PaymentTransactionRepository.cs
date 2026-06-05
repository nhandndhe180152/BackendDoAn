using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;

namespace Backend.Infrastructure.Repositories;

public class PaymentTransactionRepository : RepositoryBase<PaymentTransaction, int>, IPaymentTransactionRepository
{
    private readonly BackendContext _context;

    public PaymentTransactionRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {

        _context = context;
    }

}
