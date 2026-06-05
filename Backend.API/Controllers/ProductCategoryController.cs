using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.ProductCategories;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// Controller quản lý danh mục sản phẩm (Product Category)
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/product-category")]
    [Authorize]
    [ApiController]
    public class ProductCategoryController : BaseController, IBaseController<int, CreateProductCategoryDto, UpdateProductCategoryDto, ProductCategoryDTParameters>
    {
        private readonly IProductCategoryService _productCategoryService;

        /// Khởi tạo ProductCategoryController
        public ProductCategoryController(IProductCategoryService productCategoryService)
        {
            _productCategoryService = productCategoryService;
        }

        /// API tạo mới danh mục sản phẩm
        [HttpPost]
        //[CustomAuthorize(Enums.Menu.PRODUCT_CATEGORY, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductCategoryDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _productCategoryService.CreateAsync(obj);
            return BaseResult(result);
        }

        /// API lấy toàn bộ danh sách danh mục sản phẩm
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _productCategoryService.GetAllAsync();
            return BaseResult(result);
        }

        /// API lấy chi tiết danh mục sản phẩm theo ID
        [HttpGet("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _productCategoryService.GetByIdAsync(id);
            return BaseResult(data);
        }

        /// API tìm kiếm phân trang danh mục sản phẩm cơ bản
        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _productCategoryService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API phân trang nâng cao cho Datatable
        [HttpPost("paged-advanced")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ProductCategoryDTParameters parameters)
        {
            var data = await _productCategoryService.GetPagedAsync(parameters);
            return BaseResult(data);
        }

        /// API xóa mềm danh mục sản phẩm
        [HttpDelete("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_CATEGORY, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _productCategoryService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        /// API cập nhật thông tin danh mục sản phẩm
        [HttpPut]
        //[CustomAuthorize(Enums.Menu.PRODUCT_CATEGORY, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProductCategoryDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _productCategoryService.UpdateAsync(obj);
            return BaseResult(data);
        }

        /// API tìm kiếm lọc danh mục theo danh mục cha (ParentId)
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductCategorySearchQuery query)
        {
            var data = await _productCategoryService.GetPagedAsync(query);
            return BaseResult(data);
        }
    }
}
