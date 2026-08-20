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
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.APPROVE)]
        public async Task<IActionResult> ApproveAsync(int id, [FromBody] ApproveStockTakeDto dto)
        {
            var result = await _stockTakeService.ApproveAsync(id, dto.ApproveNote, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        /// <summary>
        /// Quét QR một bao khi đang kiểm kê. Luôn trả 200: quét nhầm bao là chuyện thường
        /// ngoài kho, màn hình cần biết bao đó thuộc lô/cột nào chứ không chỉ báo lỗi.
        /// </summary>
        [HttpPost("{id:int}/scan-bag")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.UPDATE)]
        public async Task<IActionResult> ScanBagAsync(int id, [FromBody] ScanStockTakeBagDto dto)
        {
            var result = await _stockTakeService.ScanBagAsync(id, dto, this.GetLoggedInUserId());
            return BaseResult(result);
        }

        /// <summary>Danh sách khu / cột / lô đang có bao để chọn phạm vi kiểm kê.</summary>
        [HttpGet("scope-options")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetScopeOptionsAsync([FromQuery] int warehouseId, [FromQuery] bool? quarantineOnly)
        {
            var result = await _stockTakeService.GetScopeOptionsAsync(warehouseId, quarantineOnly);
            return BaseResult(result);
        }

        /// <summary>Quét QR dán trên cột để chọn nhanh phạm vi kiểm kê.</summary>
        [HttpGet("scope-resolve")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> ResolveScopeQrAsync([FromQuery] string qrCode, [FromQuery] int? warehouseId)
        {
            var result = await _stockTakeService.ResolveScopeQrAsync(qrCode, warehouseId);
            return BaseResult(result);
        }

        /// <summary>
        /// Bản POST của scope-resolve — app quét QR nên dùng bản này: payload tem
        /// chứa ký tự '|', đi qua query string dễ bị encode/decode lệch nhau.
        /// </summary>
        [HttpPost("scope-resolve")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> ResolveScopeQrAsync([FromBody] ResolveScopeQrDto dto)
        {
            var result = await _stockTakeService.ResolveScopeQrAsync(dto?.QrCode ?? string.Empty, dto?.WarehouseId);
            return BaseResult(result);
        }

        /// <summary>Gợi ý ô cách ly / cột thường cho một bao (người dùng vẫn chọn lại được).</summary>
        [HttpGet("{id:int}/bags/{bagId:int}/target-suggestions")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.READ)]
        public async Task<IActionResult> GetBagTargetSuggestionsAsync(int id, int bagId)
        {
            var result = await _stockTakeService.GetBagTargetSuggestionsAsync(id, bagId);
            return BaseResult(result);
        }

        [HttpPut("{id:int}/reject")]
        [CustomAuthorize(Enums.Menu.STOCKTAKE, Enums.Action.APPROVE)]
        public async Task<IActionResult> RejectAsync(int id, [FromBody] RejectStockTakeDto dto)
        {
            var result = await _stockTakeService.RejectAsync(id, dto.Reason, this.GetLoggedInUserId());
            return BaseResult(result);
        }
    }
}
