using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.CustomerReturnOrderStatuses;
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
    [Route("api/v{version:apiVersion}/customer-return-order-status")]
    [ApiController]
    public class CustomerReturnOrderStatusController : BaseController, IBaseController<int, CreateCustomerReturnOrderStatusDto, UpdateCustomerReturnOrderStatusDto, DTParameter>
    {
        private readonly ICustomerReturnOrderStatusService _customerReturnOrderStatusService;
        public CustomerReturnOrderStatusController(ICustomerReturnOrderStatusService customerReturnOrderStatusService)
        {
            _customerReturnOrderStatusService = customerReturnOrderStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.CUSTOMER_RETURN_ORDER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateCustomerReturnOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _customerReturnOrderStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _customerReturnOrderStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.CUSTOMER_RETURN_ORDER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _customerReturnOrderStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _customerReturnOrderStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.CUSTOMER_RETURN_ORDER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _customerReturnOrderStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.CUSTOMER_RETURN_ORDER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _customerReturnOrderStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.CUSTOMER_RETURN_ORDER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateCustomerReturnOrderStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _customerReturnOrderStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
