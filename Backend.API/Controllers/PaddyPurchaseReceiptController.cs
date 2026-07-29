using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaddyPurchaseReceipts;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Phiếu mua lúa (PaddyPurchaseReceipt).
    /// POST /{id}/confirm — chốt phiếu, sinh lô, tạo InboundOrder.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/paddy-purchase-receipts")]
    [Authorize]
    [ApiController]
    public class PaddyPurchaseReceiptController : BaseController
    {
        private readonly IPaddyPurchaseReceiptService _receiptService;

        public PaddyPurchaseReceiptController(IPaddyPurchaseReceiptService receiptService)
        {
            _receiptService = receiptService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _receiptService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _receiptService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _receiptService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaddyPurchaseReceiptDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _receiptService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaddyPurchaseReceiptDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _receiptService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("{id}/confirm")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.UPDATE)]
        public async Task<IActionResult> ConfirmAsync(int id)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _receiptService.ConfirmReceiptAsync(id, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_PURCHASE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _receiptService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
