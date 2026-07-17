using System;
using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IMillingYieldConfigService : IServiceBase<int, CreateMillingYieldConfigDto, UpdateMillingYieldConfigDto, DTParameter>
{
}
