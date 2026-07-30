using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.InboundOrderStatuses;
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
    [Route("api/v{version:apiVersion}/inbound-order-status")]
    [ApiController]
    public class InboundOrderStatusController : BaseController, IBaseController<int, CreateInboundOrderStatusDto, UpdateInboundOrderStatusDto, DTParameter>
    {
        private readonly IInboundOrderStatusService _inboundOrderStatusService;
        public InboundOrderStatusController(IInboundOrderStatusService inboundOrderStatusService)
        {
            _inboundOrderStatusService = inboundOrderStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.INBOUND_ORDER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateInboundOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _inboundOrderStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _inboundOrderStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.INBOUND_ORDER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _inboundOrderStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _inboundOrderStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.INBOUND_ORDER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _inboundOrderStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.INBOUND_ORDER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _inboundOrderStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.INBOUND_ORDER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateInboundOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _inboundOrderStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
