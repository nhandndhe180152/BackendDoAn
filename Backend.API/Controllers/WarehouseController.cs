using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Warehouses;
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
    [Route("api/v{version:apiVersion}/warehouse")]
    [ApiController]
    public class WarehouseController : BaseController
    {
        private readonly IWarehouseService _warehouseService;

        public WarehouseController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        [HttpGet]
        // Dropdown dùng chung: bỏ CustomAuthorize READ để role không có quyền xem menu vẫn lấy được danh sách cho dropdown
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _warehouseService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _warehouseService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _warehouseService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateWarehouseDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _warehouseService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateWarehouseDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _warehouseService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _warehouseService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
} 
