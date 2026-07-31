using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.ReturnToSupplierOrderStatuses;
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
    [Route("api/v{version:apiVersion}/return-to-supplier-order-status")]
    [ApiController]
    public class ReturnToSupplierOrderStatusController : BaseController, IBaseController<int, CreateReturnToSupplierOrderStatusDto, UpdateReturnToSupplierOrderStatusDto, DTParameter>
    {
        private readonly IReturnToSupplierOrderStatusService _returnToSupplierOrderStatusService;
        public ReturnToSupplierOrderStatusController(IReturnToSupplierOrderStatusService returnToSupplierOrderStatusService)
        {
            _returnToSupplierOrderStatusService = returnToSupplierOrderStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.RETURN_TO_SUPPLIER_ORDER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateReturnToSupplierOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _returnToSupplierOrderStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _returnToSupplierOrderStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.RETURN_TO_SUPPLIER_ORDER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _returnToSupplierOrderStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _returnToSupplierOrderStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.RETURN_TO_SUPPLIER_ORDER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _returnToSupplierOrderStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.RETURN_TO_SUPPLIER_ORDER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _returnToSupplierOrderStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.RETURN_TO_SUPPLIER_ORDER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateReturnToSupplierOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _returnToSupplierOrderStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
