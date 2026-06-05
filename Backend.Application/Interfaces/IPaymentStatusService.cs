using System;
using Backend.Application.DTOs.PaymentStatuses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaymentStatusService : IServiceBase<int, CreatePaymentStatusDto, UpdatePaymentStatusDto, DTParameter>
{
}
