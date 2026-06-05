using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.IotDevices;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/iot-devices")]
    [ApiController]
    public class IotDeviceController : BaseController
    {
        private readonly IIotDeviceService _iotDeviceService;

        public IotDeviceController(IIotDeviceService iotDeviceService)
        {
            _iotDeviceService = iotDeviceService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _iotDeviceService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _iotDeviceService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _iotDeviceService.GetByIdAsync(id);

            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateIotDeviceDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();

            var result = await _iotDeviceService.CreateAsync(dto);

            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateIotDeviceDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();

            var result = await _iotDeviceService.UpdateAsync(dto);

            return BaseResult(result);
        }

        [HttpPatch("{id}/active-status")]
        public async Task<IActionResult> UpdateActiveStatusAsync(int id, [FromQuery] bool isActive)
        {
            var result = await _iotDeviceService.UpdateActiveStatusAsync(id, isActive, this.GetLoggedInUserId());

            return BaseResult(result);
        }

        [HttpPost("{id}/regenerate-api-key")]
        public async Task<IActionResult> RegenerateApiKeyAsync(int id)
        {
            var result = await _iotDeviceService.RegenerateApiKeyAsync(id, this.GetLoggedInUserId());

            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _iotDeviceService.SoftDeleteAsync(id);

            return BaseResult(result);
        }
    }
}
