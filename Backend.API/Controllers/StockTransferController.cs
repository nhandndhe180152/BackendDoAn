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
    /// POST /{id}/confirm — xác nhận, thực thi xuất/nhập tồn kho.
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

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockTransferDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _service.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateStockTransferDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _service.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("{id}/confirm")]
        public async Task<IActionResult> ConfirmAsync(int id)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _service.ConfirmTransferAsync(id, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _service.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
