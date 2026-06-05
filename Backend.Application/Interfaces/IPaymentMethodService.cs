using System;
using Backend.Application.DTOs.PaymentMethods;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IPaymentMethodService : IServiceBase<int, CreatePaymentMethodDto, UpdatePaymentMethodDto, DTParameter>
{
}
