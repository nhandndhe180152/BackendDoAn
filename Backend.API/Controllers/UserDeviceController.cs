using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.UserDevices;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/user-device")]
    [ApiController]
    public class UserDeviceController : BaseController
    {
        private readonly IUserDeviceService _userDeviceService;

        public UserDeviceController(IUserDeviceService userDeviceService)
        {
            _userDeviceService = userDeviceService;
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _userDeviceService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpPost("add-device-token")]
        public async Task<IActionResult> AddDeviceToken([FromBody] CreateUserDeviceDto dto)
        {
            dto.UserId = this.GetLoggedInUserId();
            var result = await _userDeviceService.AddDeviceToken(dto);
            return BaseResult(result);
        }

        [HttpPost("delete-device-token")]
        public async Task<IActionResult> DeleteDeviceToken([FromBody] DeleteUserDeviceDto dto)
        {
            dto.UserId = this.GetLoggedInUserId();
            var result = await _userDeviceService.DeleteDeviceToken(dto);
            return BaseResult(result);
        }

        /// <summary>Đăng ký/cập nhật thiết bị của user đang đăng nhập (gọi sau khi login).</summary>
        [HttpPost("register")]
        public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceDto dto)
        {
            dto.UserId = this.GetLoggedInUserId();
            var result = await _userDeviceService.RegisterDeviceAsync(dto);
            return BaseResult(result);
        }

        /// <summary>Danh sách thiết bị đã đăng ký của user đang đăng nhập.</summary>
        [HttpGet("my-devices")]
        public async Task<IActionResult> GetMyDevices()
        {
            var result = await _userDeviceService.GetMyDevicesAsync(this.GetLoggedInUserId());
            return BaseResult(result);
        }

        /// <summary>Đăng xuất khỏi một thiết bị cụ thể.</summary>
        [HttpPost("logout")]
        public async Task<IActionResult> LogoutDevice([FromBody] LogoutDeviceDto dto)
        {
            var result = await _userDeviceService.LogoutDeviceAsync(this.GetLoggedInUserId(), dto.DeviceId);
            return BaseResult(result);
        }

        /// <summary>Đăng xuất khỏi tất cả thiết bị khác, giữ lại thiết bị hiện tại.</summary>
        [HttpPost("logout-others")]
        public async Task<IActionResult> LogoutOtherDevices([FromBody] LogoutDeviceDto dto)
        {
            var result = await _userDeviceService.LogoutOtherDevicesAsync(this.GetLoggedInUserId(), dto.DeviceId);
            return BaseResult(result);
        }
    }
}
