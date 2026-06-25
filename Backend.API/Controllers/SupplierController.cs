using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Suppliers;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _supplierService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _supplierService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _supplierService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateSupplierDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _supplierService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateSupplierDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _supplierService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _supplierService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
