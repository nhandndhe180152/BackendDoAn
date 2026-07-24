using Asp.Versioning;
using Backend.Application.Interfaces;
using Backend.Application.DTOs.Dashboard;
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
        public async Task<IActionResult> GetSummaryAsync([FromQuery] DashboardQuery query)
        {
            var result = await _dashboardService.GetSummaryAsync(query);
            return BaseResult(result);
        }

        [HttpGet("today-tasks")]
        public async Task<IActionResult> GetTodayTasksAsync([FromQuery] DashboardQuery query)
        {
            var result = await _dashboardService.GetTodayTasksAsync(query);
            return BaseResult(result);
        }

        [HttpGet("purchase-chart")]
        public async Task<IActionResult> GetPurchaseChartAsync([FromQuery] DashboardQuery query)
        {
            var result = await _dashboardService.GetPurchaseChartAsync(query);
            return BaseResult(result);
        }

        [HttpGet("operational-efficiency")]
        public async Task<IActionResult> GetOperationalEfficiencyAsync([FromQuery] DashboardQuery query)
        {
            var result = await _dashboardService.GetOperationalEfficiencyAsync(query);
            return BaseResult(result);
        }

        [HttpGet("recent-alerts")]
        public async Task<IActionResult> GetRecentAlertsAsync([FromQuery] DashboardQuery query)
        {
            var result = await _dashboardService.GetRecentAlertsAsync(query);
            return BaseResult(result);
        }
    }
}
