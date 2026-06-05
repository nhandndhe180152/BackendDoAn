using System;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Domain.DTParameters;

namespace Backend.Application.Interfaces;

public interface IActivityLogService : IServiceBase<int, CreateActivityLogDto, UpdateActivityLogDto, ActivityLogDTParameters>
{
}
