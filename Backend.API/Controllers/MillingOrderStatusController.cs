using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.MillingOrderStatuses;
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
    [Route("api/v{version:apiVersion}/milling-order-status")]
    [ApiController]
    public class MillingOrderStatusController : BaseController, IBaseController<int, CreateMillingOrderStatusDto, UpdateMillingOrderStatusDto, DTParameter>
    {
        private readonly IMillingOrderStatusService _millingOrderStatusService;
        public MillingOrderStatusController(IMillingOrderStatusService millingOrderStatusService)
        {
            _millingOrderStatusService = millingOrderStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.MILLING_ORDER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMillingOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _millingOrderStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _millingOrderStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.MILLING_ORDER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _millingOrderStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _millingOrderStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.MILLING_ORDER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _millingOrderStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.MILLING_ORDER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _millingOrderStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.MILLING_ORDER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMillingOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _millingOrderStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
