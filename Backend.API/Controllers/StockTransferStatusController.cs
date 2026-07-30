using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.StockTransferStatuses;
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
    [Route("api/v{version:apiVersion}/stock-transfer-status")]
    [ApiController]
    public class StockTransferStatusController : BaseController, IBaseController<int, CreateStockTransferStatusDto, UpdateStockTransferStatusDto, DTParameter>
    {
        private readonly IStockTransferStatusService _stockTransferStatusService;
        public StockTransferStatusController(IStockTransferStatusService stockTransferStatusService)
        {
            _stockTransferStatusService = stockTransferStatusService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.STOCK_TRANSFER_STATUS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockTransferStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.CreatedBy = userId;
            var result = await _stockTransferStatusService.CreateAsync(obj);
            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _stockTransferStatusService.GetAllAsync();
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.STOCK_TRANSFER_STATUS, Enums.Action.READ)]
        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _stockTransferStatusService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _stockTransferStatusService.GetPagedAsync(query);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.STOCK_TRANSFER_STATUS, Enums.Action.READ)]
        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _stockTransferStatusService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [CustomAuthorize(Enums.Menu.STOCK_TRANSFER_STATUS, Enums.Action.DELETE)]
        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _stockTransferStatusService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.STOCK_TRANSFER_STATUS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateStockTransferStatusDto obj)
        {
            var userId = this.GetLoggedInUserId();
            obj.UpdatedBy = userId;
            var result = await _stockTransferStatusService.UpdateAsync(obj);
            return BaseResult(result);
        }
    }
}
