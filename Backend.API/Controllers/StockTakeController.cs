using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.StockTakes;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/stocktakes")]
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
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _stockTakeService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _stockTakeService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id:int}")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _stockTakeService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("summary")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetSummaryAsync()
        {
            var result = await _stockTakeService.GetSummaryAsync();
            return BaseResult(result);
        }

        [HttpGet("thresholds")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetThresholdsAsync()
        {
            var result = await _stockTakeService.GetThresholdsAsync();
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockTakeDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _stockTakeService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateStockTakeDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _stockTakeService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/counts")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.UPDATE)]
        public async Task<IActionResult> SaveCountsAsync(int id, [FromBody] SaveStockTakeCountsDto dto)
        {
            var result = await _stockTakeService.SaveCountsAsync(id, dto, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpPut("{id:int}/submit")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.UPDATE)]
        public async Task<IActionResult> SubmitAsync(int id, [FromBody] SubmitStockTakeDto dto)
        {
            var result = await _stockTakeService.SubmitAsync(id, dto, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _stockTakeService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/approve")]
        [HttpPost("{id:int}/approve")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.APPROVE)]
        public async Task<IActionResult> ApproveAsync(int id, [FromBody] ApproveStockTakeDto dto)
        {
            var result = await _stockTakeService.ApproveAsync(id, dto.ApproveNote, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        [HttpPut("{id:int}/reject")]
        [HttpPost("{id:int}/reject")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.APPROVE)]
        public async Task<IActionResult> RejectAsync(int id, [FromBody] RejectStockTakeDto dto)
        {
            var result = await _stockTakeService.RejectAsync(id, dto.Reason, this.GetLoggedInUserId());
            return BaseResult(result);
        }
    }
}
