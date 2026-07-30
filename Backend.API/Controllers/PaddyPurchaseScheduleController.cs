using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Lịch thu mua lúa (PaddyPurchaseSchedule).
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/paddy-purchase-schedules")]
    [Authorize]
    [ApiController]
    public class PaddyPurchaseScheduleController : BaseController
    {
        private readonly IPaddyPurchaseScheduleService _scheduleService;

        public PaddyPurchaseScheduleController(IPaddyPurchaseScheduleService scheduleService)
        {
            _scheduleService = scheduleService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _scheduleService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _scheduleService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _scheduleService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaddyPurchaseScheduleDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _scheduleService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaddyPurchaseScheduleDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _scheduleService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPatch("{id}/status")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateStatusAsync(int id, [FromQuery] string statusCode)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _scheduleService.UpdateStatusAsync(id, statusCode, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _scheduleService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
