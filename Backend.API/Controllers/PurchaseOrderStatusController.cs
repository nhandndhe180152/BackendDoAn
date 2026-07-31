using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PurchaseOrderStatuses;
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
    [Route("api/v{version:apiVersion}/purchase-order-status")]
    [ApiController]
    public class PurchaseOrderStatusController : BaseController, IBaseController<int, CreatePurchaseOrderStatusDto, UpdatePurchaseOrderStatusDto, DTParameter>
    {
        private readonly IPurchaseOrderStatusService _purchaseOrderStatusService;
        public PurchaseOrderStatusController(IPurchaseOrderStatusService purchaseOrderStatusService)
        {
            _purchaseOrderStatusService = purchaseOrderStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.PURCHASE_ORDER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePurchaseOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _purchaseOrderStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _purchaseOrderStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PURCHASE_ORDER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _purchaseOrderStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _purchaseOrderStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PURCHASE_ORDER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _purchaseOrderStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PURCHASE_ORDER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _purchaseOrderStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.PURCHASE_ORDER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePurchaseOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _purchaseOrderStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
