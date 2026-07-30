using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/activity-log")]
    [ApiController]
    public class ActivityLogController : BaseController
    {
        private readonly IActivityLogService _activityLogService;

        public ActivityLogController(IActivityLogService activityLogService)
        {
            _activityLogService = activityLogService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.ACTIVITY_LOGS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateActivityLogDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _activityLogService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.ACTIVITY_LOGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ActivityLogDTParameters parameters)
        {
            var result = await _activityLogService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpPost("me")]
        [CustomAuthorize(Enums.Menu.ACTIVITY_LOGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedByUserIdAsync([FromBody] ActivityLogDTParameters parameters)
        {
            parameters.UserId = this.GetLoggedInUserId();
            var result = await _activityLogService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

    }
}
