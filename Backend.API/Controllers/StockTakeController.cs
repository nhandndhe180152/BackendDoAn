using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.StockTakes;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/stocktake")]
    [ApiController]
    public class StockTakeController : BaseController
    {
        private readonly IStockTakeService _stockTakeService;

        public StockTakeController(IStockTakeService stockTakeService)
        {
            _stockTakeService = stockTakeService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _stockTakeService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _stockTakeService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _stockTakeService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockTakeDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _stockTakeService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateStockTakeDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _stockTakeService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _stockTakeService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPost("{id}/approve")]
        public async Task<IActionResult> ApproveAsync(int id)
        {
            var result = await _stockTakeService.ApproveAsync(id, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpPost("{id}/reject")]
        public async Task<IActionResult> RejectAsync(int id, [FromBody] RejectStockTakeDto dto)
        {
            var result = await _stockTakeService.RejectAsync(id, dto.Reason, this.GetLoggedInUserId());
            return BaseResult(result);
        }
    }
}
