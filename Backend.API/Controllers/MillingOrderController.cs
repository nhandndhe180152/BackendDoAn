using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.MillingOrders;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Lệnh xay xát (MillingOrder).
    /// POST /{id}/complete — hoàn thành lệnh xay, sinh lô gạo/phụ phẩm.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/milling-orders")]
    [Authorize]
    [ApiController]
    public class MillingOrderController : BaseController
    {
        private readonly IMillingOrderService _millingOrderService;

        public MillingOrderController(IMillingOrderService millingOrderService)
        {
            _millingOrderService = millingOrderService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _millingOrderService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _millingOrderService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _millingOrderService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMillingOrderDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _millingOrderService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMillingOrderDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _millingOrderService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("{id}/complete")]
        public async Task<IActionResult> CompleteAsync(int id)
        {
            var userId = this.GetLoggedInUserId();
            var result = await _millingOrderService.CompleteMillingOrderAsync(id, userId);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _millingOrderService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
