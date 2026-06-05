using System;
using Backend.Application.DTOs.IotDeviceCommands;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IIotDeviceCommandService : IServiceBase<int, CreateIotDeviceCommandDto, UpdateIotDeviceCommandDto, DTParameter>
{
    Task<ApiResponse> CancelAsync(int id, CancelIotDeviceCommandDto dto);
    Task<ApiResponse> GetPendingCommandForDeviceAsync(string deviceCode, string? deviceKey);
    Task<ApiResponse> CompleteCommandFromDeviceAsync(int commandId, CompleteIotDeviceCommandDto dto, string? deviceKey);
    Task<ApiResponse> MarkExpiredCommandsAsync();
}
