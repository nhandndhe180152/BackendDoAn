using System;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// Controller quản lý các biến thể sản phẩm (Product Variant)
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/product-variant")]
    [Authorize]
    [ApiController]
    public class ProductVariantController : BaseController, IBaseController<int, CreateProductVariantDto, UpdateProductVariantDto, ProductVariantDTParameters>
    {
        private readonly IProductVariantService _productVariantService;
        private readonly IQRCodeService _qrCodeService;

        /// Khởi tạo ProductVariantController
        public ProductVariantController(IProductVariantService productVariantService, IQRCodeService qrCodeService)
        {
            _productVariantService = productVariantService;
            _qrCodeService = qrCodeService;
        }

        /// API tạo mới một biến thể sản phẩm
        [HttpPost]
        //[CustomAuthorize(Enums.Menu.PRODUCT_VARIANT, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductVariantDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _productVariantService.CreateAsync(obj);
            return BaseResult(result);
        }

        /// API lấy toàn bộ danh sách các biến thể sản phẩm
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _productVariantService.GetAllAsync();
            return BaseResult(result);
        }

        /// API lấy chi tiết thông tin một biến thể sản phẩm theo ID (kèm ảnh thực tế)
        [HttpGet("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_VARIANT, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _productVariantService.GetByIdAsync(id);
            return BaseResult(data);
        }

        /// API tìm kiếm phân trang biến thể sản phẩm cơ bản
        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _productVariantService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API phân trang nâng cao cho biến thể sản phẩm (khớp DataTable)
        [HttpPost("paged-advanced")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_VARIANT, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ProductVariantDTParameters parameters)
        {
            var data = await _productVariantService.GetPagedAsync(parameters);
            return BaseResult(data);
        }

        /// API xóa mềm biến thể sản phẩm
        [HttpDelete("{id}")]
        //[CustomAuthorize(Enums.Menu.PRODUCT_VARIANT, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _productVariantService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        /// API cập nhật thông tin biến thể sản phẩm
        [HttpPut]
        //[CustomAuthorize(Enums.Menu.PRODUCT_VARIANT, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProductVariantDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _productVariantService.UpdateAsync(obj);
            return BaseResult(data);
        }

        /// API lọc nâng cao các biến thể theo ProductId hoặc trạng thái hoạt động
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] ProductVariantSearchQuery query)
        {
            var data = await _productVariantService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API tạo hình ảnh QR code cho biến thể sản phẩm theo ID
        [HttpGet("{id}/qr-code")]
        public async Task<IActionResult> GetQRCodeAsync(int id)
        {
            try
            {
                var bytes = await _qrCodeService.GenerateQRCodeImageAsync(id);
                return File(bytes, "image/png", $"qrcode-{id}.png");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// API tạo nhãn QR dạng PDF cho biến thể sản phẩm theo ID (hỗ trợ tùy chỉnh kích thước nhãn)
        [HttpGet("{id}/qr-label")]
        public async Task<IActionResult> GetQRLabelPdfAsync(int id, [FromQuery] float widthMm = 50f, [FromQuery] float heightMm = 30f)
        {
            try
            {
                var bytes = await _qrCodeService.GenerateQRLabelPdfAsync(id, widthMm, heightMm);
                return File(bytes, "application/pdf", $"qrlabel-{id}.pdf");
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// API tạo danh sách nhãn QR hàng loạt dạng file PDF
        [HttpPost("batch/qr-labels")]
        public async Task<IActionResult> GetBulkQRLabelsPdfAsync([FromBody] BatchQRLabelRequestDto request)
        {
            try
            {
                var bytes = await _qrCodeService.GenerateBulkQRLabelsPdfAsync(request);
                return File(bytes, "application/pdf", "qrlabels-batch.pdf");
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// API tạo và lưu đường dẫn QR code cho biến thể sản phẩm
        [HttpPost("{id}/generate-qr-url")]
        public async Task<IActionResult> GenerateAndSaveQRUrlAsync(int id)
        {
            try
            {
                var url = await _qrCodeService.GenerateAndSaveQRUrlAsync(id);
                return Ok(new { Url = url });
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// API đồng bộ lại toàn bộ đường dẫn QR code cho tất cả các biến thể
        [HttpPost("batch/sync-qr-urls")]
        public async Task<IActionResult> SyncAllQRCodeUrlsAsync()
        {
            try
            {
                var count = await _qrCodeService.SyncAllQRCodeUrlsAsync();
                return Ok(new { SyncedCount = count });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// API kiểm tra mã SKU khi quét QR code trong các tài liệu (như phiếu kho)
        [HttpGet("check-sku")]
        public async Task<IActionResult> CheckSkuAsync([FromQuery] string sku, [FromQuery] string? documentType = null, [FromQuery] int? documentId = null)
        {
            var result = await _productVariantService.CheckSkuAsync(sku, documentType, documentId);
            return BaseResult(result);
        }

        /// API xác nhận quét thành công mã QR để cập nhật trạng thái tài liệu liên quan
        [HttpPost("confirm-scan")]
        public async Task<IActionResult> ConfirmScanAsync([FromBody] ConfirmScanRequestDto request)
        {
            var result = await _productVariantService.ConfirmScanAsync(request);
            return BaseResult(result);
        }
    }
}
