using System;
using Backend.Application.DTOs.Wards;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IWardService : IServiceBase<int, CreateWardDto, UpdateWardDto, DTParameter>
{
    Task<ApiResponse> GetAllAsync();
    Task<ApiResponse> GetPagedAsync(DTParameter parameters);
}
