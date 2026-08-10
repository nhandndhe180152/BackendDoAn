using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Suppliers;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Nhà cung cấp (Supplier) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// Bảng phẳng nên dùng DTParameter chung.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/suppliers")]
    [Authorize]
    [ApiController]
    public class SupplierController : BaseController
    {
        private readonly ISupplierService _supplierService;

        public SupplierController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpGet]
        // Dropdown dùng chung: bỏ CustomAuthorize READ để role không có quyền xem menu vẫn lấy được danh sách cho dropdown
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _supplierService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.SUPPLIERS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _supplierService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.SUPPLIERS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _supplierService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.SUPPLIERS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateSupplierDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _supplierService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.SUPPLIERS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateSupplierDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _supplierService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.SUPPLIERS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _supplierService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
