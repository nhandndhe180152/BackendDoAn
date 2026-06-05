using Asp.Versioning;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiController]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/inventories")]
    [Authorize]
    public class InventoriesController : BaseController
    {
        private readonly IInventoryService _inventoryService;

        public InventoriesController(IInventoryService inventoryService)
        {
            _inventoryService = inventoryService;
        }

        [HttpPost("advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] InventoryDTParameters parameters)
        {
            var result = await _inventoryService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _inventoryService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("by-variant/{productVariantId:int}")]
        public async Task<IActionResult> GetByProductVariantAsync(int productVariantId)
        {
            var result = await _inventoryService.GetByProductVariantAsync(productVariantId);
            return BaseResult(result);
        }

        [HttpGet("low-stock")]
        public async Task<IActionResult> GetLowStockAsync([FromQuery] int? warehouseId, [FromQuery] int limit = 50)
        {
            var result = await _inventoryService.GetLowStockAsync(warehouseId, limit);
            return BaseResult(result);
        }
    }
}
