using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.NotificationTypes;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/notification-type")]
    [Authorize]
    [ApiController]
    public class NotificationTypeController : BaseController, IBaseController<int, CreateNotificationTypeDto, UpdateNotificationTypeDto, DTParameter>
    {
        private readonly INotificationTypeService _notificationTypeService;

        public NotificationTypeController(INotificationTypeService NotificationTypeService)
        {
            _notificationTypeService = NotificationTypeService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_TYPE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateNotificationTypeDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _notificationTypeService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _notificationTypeService.GetAllAsync();

            return BaseResult(result);
        }
        [CustomAuthorize(Enums.Menu.NOTIFICATION_TYPE, Enums.Action.READ)]
        [HttpGet("{id}")]

        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _notificationTypeService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _notificationTypeService.GetPagedAsync(query);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.NOTIFICATION_TYPE, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _notificationTypeService.GetPagedAsync(parameters);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.NOTIFICATION_TYPE, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _notificationTypeService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_TYPE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateNotificationTypeDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _notificationTypeService.UpdateAsync(obj);

            return BaseResult(data);
        }
    }
}
