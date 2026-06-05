using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Notifications;
using Backend.Application.DTOs.UserNotifications;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/notification")]
    [Authorize]
    [ApiController]
    public class NotificationController : BaseController, IBaseController<int, CreateNotificationDto, UpdateNotificationDto, NotificationDTParameters>
    {
        private readonly INotificationService _notificationService;
        public NotificationController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateNotificationDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _notificationService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _notificationService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _notificationService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _notificationService.GetPagedAsync(query);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.NOTIFICATION, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] NotificationDTParameters parameters)
        {
            var data = await _notificationService.GetPagedAsync(parameters);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.NOTIFICATION, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _notificationService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateNotificationDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _notificationService.UpdateAsync(obj);

            return BaseResult(data);
        }
        [HttpPost("me")]
        public async Task<IActionResult> GetUserNotifications([FromBody] UserNotificationsSearchQuery query)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _notificationService.GetPagedByUserAsync(userId, query);
            return BaseResult(result);
        }

        [HttpPut("me/{userNotificationId}/mark-read")]
        public async Task<IActionResult> MarkRead(int userNotificationId)
        {
            var result = await _notificationService.UpdateStatusAsync(userNotificationId, true);

            return BaseResult(result);
        }

        [HttpPut("me/{userNotificationId}/mark-unread")]
        public async Task<IActionResult> MarkUnread(int userNotificationId)
        {
            var result = await _notificationService.UpdateStatusAsync(userNotificationId, false);

            return BaseResult(result);
        }

        [HttpDelete("me/{userNotificationId}")]
        public async Task<IActionResult> SoftDeleteUserNotificationAsync(int userNotificationId)
        {
            var result = await _notificationService.SoftDeleteUserNotificationAsync(userNotificationId);

            return BaseResult(result);
        }

        [HttpGet("test-fireBase")]
        [AllowAnonymous]
        public async Task<IActionResult> TestFireBase(int userId)
        {
            var result = await _notificationService.TestFireBase(userId);
            return BaseResult(result);
        }
    }
}
