using Asp.Versioning;
using Backend.Application.DTOs.InventoryTransactions;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiController]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/inventory-transactions")]
    [Authorize]
    public class InventoryTransactionsController : BaseController
    {
        private readonly IInventoryTransactionService _inventoryTransactionService;

        public InventoryTransactionsController(IInventoryTransactionService inventoryTransactionService)
        {
            _inventoryTransactionService = inventoryTransactionService;
        }

        [HttpPost("advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] InventoryTransactionDTParameters parameters)
        {
            var result = await _inventoryTransactionService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _inventoryTransactionService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("by-variant/{productVariantId:int}")]
        public async Task<IActionResult> GetByProductVariantAsync(int productVariantId, [FromQuery] int limit = 100)
        {
            var result = await _inventoryTransactionService.GetByProductVariantAsync(productVariantId, limit);
            return BaseResult(result);
        }

        [HttpPost("manual-adjustment")]
        public async Task<IActionResult> ManualAdjustmentAsync([FromBody] ManualInventoryAdjustmentDto request)
        {
            var result = await _inventoryTransactionService.ManualAdjustmentAsync(request);
            return BaseResult(result);
        }
    }
}
