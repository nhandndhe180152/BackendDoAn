using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Alerts;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Cảnh báo & rule-based warnings (SCR-21): liệt kê, chi tiết, tổng hợp KPI,
    /// ghi nhận (acknowledge), xử lý (resolve) và xoá mềm. Alert do các background job sinh ra.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/alerts")]
    [Authorize]
    [ApiController]
    public class AlertController : BaseController
    {
        private readonly IAlertService _alertService;

        public AlertController(IAlertService alertService)
        {
            _alertService = alertService;
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _alertService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryAsync()
        {
            var result = await _alertService.GetSummaryAsync();
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _alertService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPut("{id}/acknowledge")]
        public async Task<IActionResult> AcknowledgeAsync(int id)
        {
            var result = await _alertService.AcknowledgeAsync(id, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpPut("{id}/resolve")]
        public async Task<IActionResult> ResolveAsync(int id)
        {
            var result = await _alertService.ResolveAsync(id, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _alertService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        /// <summary>Đánh dấu tất cả cảnh báo đang mở là đã đọc/ghi nhận.</summary>
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllReadAsync()
        {
            var result = await _alertService.MarkAllReadAsync(this.GetLoggedInUserId());
            return BaseResult(result);
        }

        /// <summary>Danh sách quy tắc cảnh báo + trạng thái bật/tắt (khối "Quy tắc cảnh báo").</summary>
        [HttpGet("rules")]
        public async Task<IActionResult> GetRulesAsync()
        {
            var result = await _alertService.GetRulesAsync();
            return BaseResult(result);
        }

        /// <summary>Bật/tắt một quy tắc cảnh báo theo mã.</summary>
        [HttpPut("rules/{code}")]
        public async Task<IActionResult> ToggleRuleAsync(string code, [FromBody] ToggleAlertRuleDto body)
        {
            var result = await _alertService.ToggleRuleAsync(code, body.Enabled, this.GetLoggedInUserId());
            return BaseResult(result);
        }
    }
}
