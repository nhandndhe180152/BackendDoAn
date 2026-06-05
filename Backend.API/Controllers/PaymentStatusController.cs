using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaymentStatuses;
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
    [Route("api/v{version:apiVersion}/payment-status")]
    [ApiController]
    public class PaymentStatusController : BaseController,
                 IBaseController<int, CreatePaymentStatusDto, UpdatePaymentStatusDto, DTParameter>
    {
        private readonly IPaymentStatusService _paymentStatusService;
        public PaymentStatusController(IPaymentStatusService paymentStatusService)
        {
            _paymentStatusService = paymentStatusService;
        }
        [HttpPost]
        [CustomAuthorize(Enums.Menu.PAYMENT_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaymentStatusDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _paymentStatusService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _paymentStatusService.GetAllAsync();

            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.PAYMENT_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _paymentStatusService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _paymentStatusService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [CustomAuthorize(Enums.Menu.PAYMENT_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _paymentStatusService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [CustomAuthorize(Enums.Menu.PAYMENT_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _paymentStatusService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [CustomAuthorize(Enums.Menu.PAYMENT_STATUS, Enums.Action.UPDATE)]
        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaymentStatusDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _paymentStatusService.UpdateAsync(obj);

            return BaseResult(data);
        }
    }
}
