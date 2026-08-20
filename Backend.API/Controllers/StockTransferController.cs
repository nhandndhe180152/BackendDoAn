using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.StockTransfers;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Phiếu điều chuyển nội bộ (StockTransfer).
    /// Xuất kho nguồn và nhận kho đích được thực hiện ở hai bước riêng biệt.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/stock-transfers")]
    [Authorize]
    [ApiController]
    public class StockTransferController : BaseController
    {
        private readonly IStockTransferService _service;

        public StockTransferController(IStockTransferService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _service.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _service.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("summary")]
        public async Task<IActionResult> GetSummaryAsync()
        {
            var result = await _service.GetSummaryAsync();
            return BaseResult(result);
        }

        /// <summary>Bao ở đỉnh cột nguồn có thể chọn để chuyển kho theo BAO (picker).</summary>
        [HttpGet("source-bags")]
        public async Task<IActionResult> GetSourceBagsAsync(
            [FromQuery] int fromWarehouseId,
            [FromQuery] int fromLocationId,
            [FromQuery] int? productVariantId)
        {
            var result = await _service.GetSourceBagsAsync(fromWarehouseId, fromLocationId, productVariantId);
            return BaseResult(result);
        }

        /// <summary>Gợi ý ô lưu ở kho đích (chọn ô tốt nhất) để tự điền vị trí đích.</summary>
        [HttpGet("destination-suggestions")]
        public async Task<IActionResult> GetDestinationSuggestionsAsync(
            [FromQuery] int toWarehouseId,
            [FromQuery] int productVariantId,
            [FromQuery] decimal weightKg = 0)
        {
            var result = await _service.GetDestinationSuggestionsAsync(toWarehouseId, productVariantId, weightKg);
            return BaseResult(result);
        }

        /// <summary>Gợi ý ô cách ly ở kho nguồn (cho bao không đạt chất lượng).</summary>
        [HttpGet("quarantine-suggestions")]
        public async Task<IActionResult> GetQuarantineSuggestionsAsync(
            [FromQuery] int fromWarehouseId,
            [FromQuery] int productVariantId,
            [FromQuery] decimal weightKg = 0)
        {
            var result = await _service.GetQuarantineSuggestionsAsync(fromWarehouseId, productVariantId, weightKg);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockTransferDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _service.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateAsync(int id, [FromBody] UpdateStockTransferDto dto)
        {
            dto.Id = id;
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _service.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/dispatch")]
        public async Task<IActionResult> DispatchAsync(int id)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _service.DispatchAsync(id, userId);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/receive")]
        public async Task<IActionResult> ReceiveAsync(int id)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _service.ReceiveAsync(id, userId);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/cancel")]
        public async Task<IActionResult> CancelAsync(int id, [FromBody] CancelStockTransferDto? dto)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _service.CancelAsync(id, dto?.Reason, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _service.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
