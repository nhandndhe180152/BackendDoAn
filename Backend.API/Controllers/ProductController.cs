using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Products;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// Controller quản lý các API liên quan đến sản phẩm (Product)
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/product")]
    [Authorize]
    [ApiController]
    public class ProductController : BaseController, IBaseController<int, CreateProductDto, UpdateProductDto, ProductDTParameters>
    {
        private readonly IProductService _productService;

        /// Khởi tạo ProductController với Service tương ứng
        public ProductController(IProductService productService)
        {
            _productService = productService;
        }

        /// API Tạo mới sản phẩm
        [HttpPost]
        //[CustomAuthorize(Enums.Menu.PRODUCT, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductDto obj)
        {
            // Tự động gán UserId của người tạo từ Token đăng nhập
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _productService.CreateAsync(obj);
            return BaseResult(result);
        }

        /// API Lấy toàn bộ danh sách sản phẩm (không phân trang)
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _productService.GetAllAsync();
            return BaseResult(result);
        }

        /// API Lấy thông tin chi tiết một sản phẩm theo ID
        [HttpGet("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _productService.GetByIdAsync(id);
            return BaseResult(data);
        }

        /// API Tìm kiếm và phân trang sản phẩm theo từ khóa (dùng cho tìm kiếm đơn giản)
        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _productService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API Phân trang nâng cao tích hợp với DataTable ở Client
        [HttpPost("paged-advanced")]
        //[CustomAuthorize(Enums.Menu.PRODUCT, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ProductDTParameters parameters)
        {
            var data = await _productService.GetPagedAsync(parameters);
            return BaseResult(data);
        }

        /// API Xóa mềm sản phẩm theo ID (IsDeleted = true)
        [HttpDelete("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _productService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        /// API Cập nhật thông tin sản phẩm
        [HttpPut]
        //[CustomAuthorize(Enums.Menu.PRODUCT, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProductDto obj)
        {
            // Tự động gán UserId của người cập nhật từ Token đăng nhập
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _productService.UpdateAsync(obj);
            return BaseResult(data);
        }

        /// API Lọc sản phẩm nâng cao theo danh mục và trạng thái hoạt động
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductSearchQuery query)
        {
            var data = await _productService.GetPagedAsync(query);
            return BaseResult(data);
        }
    }
}
