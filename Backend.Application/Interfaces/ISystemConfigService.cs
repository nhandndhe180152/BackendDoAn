using System;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface ISystemConfigService : IServiceBase<int, CreateSystemConfigDto, UpdateSystemConfigDto, DTParameter>
{
    Task<string> GetValueByKey(string key);
    Task<ApiResponse> GetContactInformationAsync();
    Task<ApiResponse> GetPrivacyPolicyAsync();
    Task<ApiResponse> GetTermOfServiceAsync();
}
