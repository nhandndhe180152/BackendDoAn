using System;
using Backend.Application.DTOs.AuditLogs;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IAuditLogService : IServiceBase<int, CreateAuditLogDto, UpdateAuditLogDto, AuditLogDTParameters>
{
    ApiResponse GetListAction();
    ApiResponse GetListAuditEntity();
}
