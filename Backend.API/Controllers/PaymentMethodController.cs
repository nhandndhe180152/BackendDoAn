using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaymentMethods;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/payment-method")]
    [Authorize]
    [ApiController]
    public class PaymentMethodController : BaseController, IBaseController<int, CreatePaymentMethodDto, UpdatePaymentMethodDto, DTParameter>
    {
        private readonly IPaymentMethodService _PaymentMethodService;

        public PaymentMethodController(IPaymentMethodService PaymentMethodService)
        {
            _PaymentMethodService = PaymentMethodService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.PAYMENT_METHOD, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaymentMethodDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _PaymentMethodService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _PaymentMethodService.GetAllAsync();

            return BaseResult(result);
        }
        [CustomAuthorize(Enums.Menu.PAYMENT_METHOD, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _PaymentMethodService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _PaymentMethodService.GetPagedAsync(query);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.PAYMENT_METHOD, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _PaymentMethodService.GetPagedAsync(parameters);

            return BaseResult(data);
        }
        [CustomAuthorize(Enums.Menu.PAYMENT_METHOD, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _PaymentMethodService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.PAYMENT_METHOD, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaymentMethodDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _PaymentMethodService.UpdateAsync(obj);

            return BaseResult(data);
        }
    }
}
