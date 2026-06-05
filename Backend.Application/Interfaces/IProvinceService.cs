using System;
using Backend.Application.DTOs.Provinces;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IProvinceService : IServiceBase<int, CreateProvinceDto, UpdateProvinceDto, DTParameter>
{
    Task<ApiResponse> SyncDataAsync();
    Task<ApiResponse> GetAllAsync();
    Task<ApiResponse> GetPagedAsync(DTParameter parameters);
    Task<ApiResponse> GetWardsAsync(int provinceId);
}
