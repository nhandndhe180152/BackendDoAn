using System;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUnitOfMeasureService : IServiceBase<int, CreateUnitOfMeasureDto, UpdateUnitOfMeasureDto, DTParameter>
{
}
