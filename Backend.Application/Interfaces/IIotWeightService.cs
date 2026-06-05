using System;
using Backend.Application.DTOs.IotWeights;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IIotWeightService
{
    Task<ApiResponse> ReceiveWeightAsync(ReceiveIotWeightDto dto, string? deviceKey, string? requestIpAddress);

    Task<ApiResponse> GetLatestWeightAsync(string deviceCode);

    Task<ApiResponse> AttachContextAsync(int weightLogId, AttachIotWeightContextDto dto);
}
