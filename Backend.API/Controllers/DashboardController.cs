using Asp.Versioning;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/dashboard")]
    [ApiController]
    public class DashboardController : BaseController
    {
        private readonly IDashboardService _dashboardService;

        public DashboardController(IDashboardService dashboardService)
        {
            _dashboardService = dashboardService;
        }

        [HttpGet("report-statistics")]
        public async Task<IActionResult> GetReportStatisticsAsync([FromQuery] string period)
        {
            var result = await _dashboardService.GetReportStatisticsAsync(period);
            return BaseResult(result);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryAsync([FromQuery] Backend.Application.DTOs.Dashboard.DashboardQuery query)
        {
            var result = await _dashboardService.GetSummaryAsync(query);
            return BaseResult(result);
        }
    }
}
