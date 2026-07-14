using System;
using Backend.Application.DTOs.Farmers;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IFarmerService : IServiceBase<int, CreateFarmerDto, UpdateFarmerDto, DTParameter>
{
}
