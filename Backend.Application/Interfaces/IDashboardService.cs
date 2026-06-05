using System;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IDashboardService
{
    Task<ApiResponse> GetReportStatisticsAsync(string period);
}
