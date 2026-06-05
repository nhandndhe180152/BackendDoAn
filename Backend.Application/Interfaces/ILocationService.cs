using System;
using Backend.Application.DTOs.Locations;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ILocationService : IServiceBase<int, CreateLocationDto, UpdateLocationDto, DTParameter>
{
}
