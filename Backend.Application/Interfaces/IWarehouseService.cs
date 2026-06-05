using System;
using Backend.Application.DTOs.Warehouses;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IWarehouseService : IServiceBase<int, CreateWarehouseDto, UpdateWarehouseDto, DTParameter>
{
}
