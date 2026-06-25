using System;
using Backend.Application.DTOs.Suppliers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISupplierService : IServiceBase<int, CreateSupplierDto, UpdateSupplierDto, DTParameter>
{
}
