using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.NotificationCategories;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/notification-category")]
    [Authorize]
    [ApiController]
    public class NotificationCategoryController : BaseController,
        IBaseController<int, CreateNotificationCategoryDto, UpdateNotificationCategoryDto, DTParameter>
    {
        private readonly INotificationCategoryService _notificationCategoryService;

        public NotificationCategoryController(INotificationCategoryService notificationCategoryService)
        {
            _notificationCategoryService = notificationCategoryService;
        }

        [HttpPost]
        //[CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY,Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateNotificationCategoryDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var data = await _notificationCategoryService.CreateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var data = await _notificationCategoryService.GetAllAsync();

            return BaseResult(data);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _notificationCategoryService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _notificationCategoryService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _notificationCategoryService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var reuslt = await _notificationCategoryService.SoftDeleteAsync(id);

            return BaseResult(reuslt);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateNotificationCategoryDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var reuslt = await _notificationCategoryService.UpdateAsync(obj);

            return BaseResult(reuslt);
        }

        [HttpPut("update-list")]
        [CustomAuthorize(Enums.Menu.NOTIFICATION_CATEGORY, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateListAsync([FromBody] List<UpdateNotificationCategoryDto> objs)
        {
            var result = await _notificationCategoryService.UpdateListAsync(objs);

            return BaseResult(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> SearchNotificationCategory([FromQuery] NotificationCategorySearchQuery query)
        {
            var data = await _notificationCategoryService.GetPagedAsync(query);

            return BaseResult(data);
        }
    }
}
