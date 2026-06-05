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
    }
}
