using System;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IRiceVarietyService : IServiceBase<int, CreateRiceVarietyDto, UpdateRiceVarietyDto, DTParameter>
{
}
