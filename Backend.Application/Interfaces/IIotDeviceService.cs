using System;
using Backend.Application.DTOs.IotDevices;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IIotDeviceService : IServiceBase<int, CreateIotDeviceDto, UpdateIotDeviceDto, DTParameter>
{

    Task<ApiResponse> RegenerateApiKeyAsync(int id, int? updatedBy);

    Task<ApiResponse> UpdateActiveStatusAsync(int id, bool isActive, int? updatedBy);
}
