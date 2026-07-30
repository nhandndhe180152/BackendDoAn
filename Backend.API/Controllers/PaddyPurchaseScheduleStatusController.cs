using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaddyPurchaseScheduleStatuses;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/paddy-purchase-schedule-status")]
    [ApiController]
    public class PaddyPurchaseScheduleStatusController : BaseController, IBaseController<int, CreatePaddyPurchaseScheduleStatusDto, UpdatePaddyPurchaseScheduleStatusDto, DTParameter>
    {
        private readonly IPaddyPurchaseScheduleStatusService _paddyPurchaseScheduleStatusService;
        public PaddyPurchaseScheduleStatusController(IPaddyPurchaseScheduleStatusService paddyPurchaseScheduleStatusService)
        {
            _paddyPurchaseScheduleStatusService = paddyPurchaseScheduleStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.PADDY_PURCHASE_SCHEDULE_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaddyPurchaseScheduleStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _paddyPurchaseScheduleStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _paddyPurchaseScheduleStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PADDY_PURCHASE_SCHEDULE_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _paddyPurchaseScheduleStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _paddyPurchaseScheduleStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PADDY_PURCHASE_SCHEDULE_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _paddyPurchaseScheduleStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PADDY_PURCHASE_SCHEDULE_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _paddyPurchaseScheduleStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.PADDY_PURCHASE_SCHEDULE_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaddyPurchaseScheduleStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _paddyPurchaseScheduleStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
