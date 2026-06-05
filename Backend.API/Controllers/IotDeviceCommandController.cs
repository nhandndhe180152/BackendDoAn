using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.IotDeviceCommands;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/iot-device-commands")]
    [ApiController]
    public class IotDeviceCommandController : BaseController
    {
        private readonly IIotDeviceCommandService _iotDeviceCommandService;

        public IotDeviceCommandController(IIotDeviceCommandService iotDeviceCommandService)
        {
            _iotDeviceCommandService = iotDeviceCommandService;
        }

        // ===================== ADMIN / FLUTTER / WEB ENDPOINTS =====================

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _iotDeviceCommandService.GetAllAsync();
            return BaseResult(result);
        }

        [Authorize]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _iotDeviceCommandService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [Authorize]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _iotDeviceCommandService.GetByIdAsync(id);
            return BaseResult(result);
        }

        // Dùng cho Web/Flutter gửi lệnh TARE/RESET/CALIBRATE cho ESP32.
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateIotDeviceCommandDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _iotDeviceCommandService.CreateAsync(dto);
            return BaseResult(result);
        }

        [Authorize]
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateIotDeviceCommandDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _iotDeviceCommandService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [Authorize]
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> CancelAsync(int id, [FromBody] CancelIotDeviceCommandDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _iotDeviceCommandService.CancelAsync(id, dto);
            return BaseResult(result);
        }

        [Authorize]
        [HttpPatch("mark-expired")]
        public async Task<IActionResult> MarkExpiredCommandsAsync()
        {
            var result = await _iotDeviceCommandService.MarkExpiredCommandsAsync();
            return BaseResult(result);
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _iotDeviceCommandService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        // ===================== ESP32 ENDPOINTS =====================
        // ESP32 không dùng JWT. Bảo vệ bằng header X-Device-Key.

        [AllowAnonymous]
        [HttpGet("devices/{deviceCode}/pending")]
        public async Task<IActionResult> GetPendingCommandForDeviceAsync(
            string deviceCode,
            [FromHeader(Name = "X-Device-Key")] string? deviceKey)
        {
            var result = await _iotDeviceCommandService.GetPendingCommandForDeviceAsync(deviceCode, deviceKey);
            return BaseResult(result);
        }

        [AllowAnonymous]
        [HttpPost("{commandId}/complete")]
        public async Task<IActionResult> CompleteCommandFromDeviceAsync(
            int commandId,
            [FromBody] CompleteIotDeviceCommandDto dto,
            [FromHeader(Name = "X-Device-Key")] string? deviceKey)
        {
            var result = await _iotDeviceCommandService.CompleteCommandFromDeviceAsync(commandId, dto, deviceKey);
            return BaseResult(result);
        }
    }
}
