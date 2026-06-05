using System;
using Backend.Application.DTOs.PaymentTransactions;
using Backend.Domain.DTParameters;

namespace Backend.Application.Interfaces;

public interface IPaymentTransactionService : IServiceBase<int, CreatePaymentTransactionDto, UpdatePaymentTransactionDto, PaymentTransactionDTParameters>
{
}
