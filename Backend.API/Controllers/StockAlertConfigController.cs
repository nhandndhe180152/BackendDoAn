using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.StockAlertConfigs;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Cấu hình ngưỡng cảnh báo tồn thấp theo kho/SKU (SCR-20). CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/stock-alert-configs")]
    [Authorize]
    [ApiController]
    public class StockAlertConfigController : BaseController
    {
        private readonly IStockAlertConfigService _stockAlertConfigService;

        public StockAlertConfigController(IStockAlertConfigService stockAlertConfigService)
        {
            _stockAlertConfigService = stockAlertConfigService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _stockAlertConfigService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _stockAlertConfigService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _stockAlertConfigService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateStockAlertConfigDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _stockAlertConfigService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateStockAlertConfigDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _stockAlertConfigService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.STOCK_ALERT_CONFIGS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _stockAlertConfigService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
