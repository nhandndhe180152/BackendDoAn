using System;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class DashboardService : IDashboardService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DashboardService> _logger;


    public DashboardService(ILogger<DashboardService> logger, IHttpContextAccessor httpContextAccessor)
    {
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<ApiResponse> GetReportStatisticsAsync(string period)
    {
        throw new NotImplementedException();
    }
}