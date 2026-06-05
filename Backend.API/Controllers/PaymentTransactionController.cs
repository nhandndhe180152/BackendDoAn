using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaymentTransactions;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/payment-transaction")]
    [Authorize]
    [ApiController]
    public class PaymentTransactionController : BaseController, IBaseController<int, CreatePaymentTransactionDto, UpdatePaymentTransactionDto, PaymentTransactionDTParameters>
    {
        private readonly IPaymentTransactionService _paymentTransactionService;
        public PaymentTransactionController(IPaymentTransactionService paymentTransactionService)
        {
            _paymentTransactionService = paymentTransactionService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaymentTransactionDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _paymentTransactionService.CreateAsync(obj);

            return BaseResult(result);
        }
        [HttpGet]
        public Task<IActionResult> GetAllAsync()
        {
            throw new NotImplementedException();
        }
        [CustomAuthorize(Enums.Menu.PAYMENT_TRANSACTION, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _paymentTransactionService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            throw new NotImplementedException();
        }
        [CustomAuthorize(Enums.Menu.PAYMENT_TRANSACTION, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] PaymentTransactionDTParameters parameters)
        {
            var data = await _paymentTransactionService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        public Task<IActionResult> SoftDeleteAsync(int id)
        {
            throw new NotImplementedException();
        }
        [HttpPut]
        public Task<IActionResult> UpdateAsync([FromBody] UpdatePaymentTransactionDto obj)
        {
            throw new NotImplementedException();
        }
    }
}
