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
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProductVariantDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _productVariantService.CreateAsync(obj);
            return BaseResult(result);
        }

        /// API lấy toàn bộ danh sách các biến thể sản phẩm
        [HttpGet]
        // Dropdown dùng chung: bỏ CustomAuthorize READ để role không có quyền xem menu vẫn lấy được danh sách cho dropdown
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _productVariantService.GetAllAsync();
            return BaseResult(result);
        }

        /// API lấy chi tiết thông tin một biến thể sản phẩm theo ID (kèm ảnh thực tế)
        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _productVariantService.GetByIdAsync(id);
            return BaseResult(data);
        }

        /// API tìm kiếm phân trang biến thể sản phẩm cơ bản
        [HttpPost("paged")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _productVariantService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API phân trang nâng cao cho biến thể sản phẩm (khớp DataTable)
        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] ProductVariantDTParameters parameters)
        {
            var data = await _productVariantService.GetPagedAsync(parameters);
            return BaseResult(data);
        }

        /// API xóa mềm biến thể sản phẩm
        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _productVariantService.SoftDeleteAsync(id);
            return BaseResult(data);
        }

        /// API cập nhật thông tin biến thể sản phẩm
        [HttpPut]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProductVariantDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _productVariantService.UpdateAsync(obj);
            return BaseResult(data);
        }

        /// API lọc nâng cao các biến thể theo ProductId hoặc trạng thái hoạt động
        [HttpGet("search")]
        // Dropdown dùng chung: bỏ CustomAuthorize READ để role không có quyền xem menu vẫn lấy được danh sách cho dropdown
        public async Task<IActionResult> Search([FromQuery] ProductVariantSearchQuery query)
        {
            var data = await _productVariantService.GetPagedAsync(query);
            return BaseResult(data);
        }

        /// API lấy URL QR code đã lưu của biến thể sản phẩm theo ID
        [HttpGet("{id}/qr-code")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetQRCodeAsync(int id)
        {
            var data = await _productVariantService.GetQrCodeUrlAsync(id);
            return BaseResult(data);
        }

        /// API tạo nhãn QR dạng PDF cho biến thể sản phẩm theo ID (hỗ trợ tùy chỉnh kích thước nhãn)
        [HttpGet("{id}/qr-label")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
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
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetBulkQRLabelsPdfAsync([FromBody] BatchQRLabelRequestDto request)
        {
            if (request == null || request.Items == null || !request.Items.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách sản phẩm in nhãn không được rỗng."));
            }

            if (request.Items.Sum(x => Math.Max(1, (int)Math.Round((decimal)x.Quantity, MidpointRounding.AwayFromZero))) > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Tổng số lượng nhãn in không được vượt quá 500."));
            }

            try
            {
                var bytes = await _qrCodeService.GenerateBulkQRLabelsPdfAsync(request);
                return File(bytes, "application/pdf", "qrlabels-batch.pdf");
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Error(message: ex.Message, status: 500, code: "CMN_500"));
            }
        }

        /// API tạo và lưu đường dẫn QR code cho biến thể sản phẩm
        [HttpPost("{id}/generate-qr-url")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> GenerateAndSaveQRUrlAsync(int id)
        {
            try
            {
                var url = await _qrCodeService.GenerateAndSaveQRUrlAsync(id);
                return Ok(ApiResponse.Success(new { Url = url }));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Error(message: ex.Message, status: 500, code: "CMN_500"));
            }
        }

        /// API đồng bộ lại toàn bộ đường dẫn QR code cho tất cả các biến thể
        [HttpPost("batch/sync-qr-urls")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
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
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> CheckSkuAsync([FromQuery] string sku, [FromQuery] string? documentType = null, [FromQuery] int? documentId = null)
        {
            var result = await _productVariantService.CheckSkuAsync(sku, documentType, documentId);
            return BaseResult(result);
        }

        /// API xác nhận quét thành công mã QR để cập nhật trạng thái tài liệu liên quan
        [HttpPost("confirm-scan")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> ConfirmScanAsync([FromBody] ConfirmScanRequestDto request)
        {
            var result = await _productVariantService.ConfirmScanAsync(request);
            return BaseResult(result);
        }

        /// API kích hoạt biến thể sản phẩm (IsActive = true)
        [HttpPost("{id}/activate")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> ActivateAsync(int id)
        {
            var data = await _productVariantService.ActivateAsync(id, this.GetLoggedInUserId());
            return BaseResult(data);
        }

        /// API vô hiệu hóa biến thể sản phẩm (IsActive = false)
        [HttpPost("{id}/deactivate")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> DeactivateAsync(int id)
        {
            var data = await _productVariantService.DeactivateAsync(id, this.GetLoggedInUserId());
            return BaseResult(data);
        }

        /// API tạo và lưu URL QR code cho biến thể sản phẩm (thông qua service)
        [HttpPost("{id}/generate-qr")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.UPDATE)]
        public async Task<IActionResult> GenerateQrAsync(int id)
        {
            var data = await _productVariantService.GenerateQrAsync(id, this.GetLoggedInUserId());
            return BaseResult(data);
        }

        /// API tra cứu biến thể sản phẩm theo mã SKU (không liên kết tài liệu)
        [HttpGet("by-sku/{sku}")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetBySkuAsync(string sku)
        {
            var data = await _productVariantService.CheckSkuAsync(sku);
            return BaseResult(data);
        }

        /// API tra cứu biến thể sản phẩm theo mã QR Code
        [HttpGet("by-qr")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetByQrCodeAsync([FromQuery] string qrCode)
        {
            var data = await _productVariantService.GetByQrCodeAsync(qrCode);
            return BaseResult(data);
        }
    }
}
