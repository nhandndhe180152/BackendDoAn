using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.ProductAttributes;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// Controller quản lý các thuộc tính sản phẩm (Product Attribute) như Kích thước, Màu sắc...
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/product-attribute")]
    [Authorize]
    [ApiController]
    public class ProductAttributeController : BaseController, IBaseController<int, CreateProductAttributeDto, UpdateProductAttributeDto, ProductAttributeDTParameters>
    {
        private readonly IProductAttributeService _productAttributeService;

        /// Khởi tạo ProductAttributeController
        public ProductAttributeController(IProductAttributeService productAttributeService)
        {
            _productAttributeService = productAttributeService;
        }

        /// API tạo mới một thuộc tính sản phẩm
        [HttpPost]
        //[CustomAuthorize(Enums.Menu.PRODUCT_ATTRIBUTE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductAttributeDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _productAttributeService.CreateAsync(obj);
            return BaseResult(result);
        }

        /// API lấy toàn bộ danh sách các thuộc tính sản phẩm
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _productAttributeService.GetAllAsync();
            return BaseResult(result);
        }

        /// API lấy chi tiết thông tin một thuộc tính sản phẩm theo ID
        [HttpGet("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_ATTRIBUTE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _productAttributeService.GetByIdAsync(id);
            return BaseResult(data);
        }

        /// API tìm kiếm phân trang thuộc tính sản phẩm cơ bản
        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _productAttributeService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API phân trang nâng cao cho thuộc tính sản phẩm (khớp DataTable)
        [HttpPost("paged-advanced")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_ATTRIBUTE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ProductAttributeDTParameters parameters)
        {
            var data = await _productAttributeService.GetPagedAsync(parameters);
            return BaseResult(data);
        }

        /// API xóa mềm thuộc tính sản phẩm (IsDeleted = true)
        [HttpDelete("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_ATTRIBUTE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _productAttributeService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        /// API cập nhật thông tin thuộc tính sản phẩm
        [HttpPut]
        //[CustomAuthorize(Enums.Menu.PRODUCT_ATTRIBUTE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProductAttributeDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _productAttributeService.UpdateAsync(obj);
            return BaseResult(data);
        }

        /// API lọc nâng cao thuộc tính sản phẩm
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductAttributeSearchQuery query)
        {
            var data = await _productAttributeService.GetPagedAsync(query);
            return BaseResult(data);
        }
    }
}
