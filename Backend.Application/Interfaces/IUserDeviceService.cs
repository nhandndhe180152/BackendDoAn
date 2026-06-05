using System;
using Backend.Application.DTOs.UserDevices;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserDeviceService : IServiceBase<int, CreateUserDeviceDto, UpdateUserDeviceDto, DTParameter>
{
    Task<ApiResponse> AddDeviceToken(CreateUserDeviceDto dto);
    Task<ApiResponse> DeleteDeviceToken(DeleteUserDeviceDto dto);
}
