using Asp.Versioning;
using Backend.Application.DTOs.IotWeights;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/iot")]
    [ApiController]
    public class IotController : BaseController
    {
        private readonly IIotWeightService _iotWeightService;

        public IotController(IIotWeightService iotWeightService)
        {
            _iotWeightService = iotWeightService;
        }

        [AllowAnonymous]
        [HttpPost("weight")]
        public async Task<IActionResult> ReceiveWeight(
            [FromBody] ReceiveIotWeightDto dto,
            [FromHeader(Name = "X-Device-Key")] string? deviceKey)
        {
            var requestIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            var result = await _iotWeightService.ReceiveWeightAsync(dto, deviceKey, requestIp);

            return BaseResult(result);
        }

        [Authorize]
        [HttpGet("devices/{deviceCode}/latest-weight")]
        public async Task<IActionResult> GetLatestWeight(string deviceCode)
        {
            var result = await _iotWeightService.GetLatestWeightAsync(deviceCode);

            return BaseResult(result);
        }

        /// <summary>
        /// Web/Flutter gắn context nghiệp vụ cho một weight log.
        /// ESP32 không biết đang cân phiếu nào, nên mobile/web sẽ gọi endpoint này
        /// sau khi người dùng bấm "Xác nhận cân" trong màn nhập/xuất/kiểm kho.
        /// </summary>
        [Authorize]
        [HttpPost("weight-logs/{weightLogId:int}/attach-context")]
        public async Task<IActionResult> AttachContext(
            int weightLogId,
            [FromBody] AttachIotWeightContextDto dto)
        {
            var result = await _iotWeightService.AttachContextAsync(weightLogId, dto);

            return BaseResult(result);
        }
    }
}
