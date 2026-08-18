using System;
using Backend.Application.DTOs.Customers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ICustomerService : IServiceBase<int, CreateCustomerDto, UpdateCustomerDto, DTParameter>
{
}
