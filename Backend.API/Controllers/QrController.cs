using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.Application.Interfaces;
using Backend.Application.DTOs.QrCode;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.API.Utilities;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}")]
    [ApiController]
    public class QrController : BaseController
    {
        private readonly IQRCodeService _qrCodeService;

        public QrController(IQRCodeService qrCodeService)
        {
            _qrCodeService = qrCodeService;
        }

        [HttpGet("qr/resolve")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> ResolveQrGetAsync(
            [FromQuery] string payload,
            [FromQuery] string? operation,
            [FromQuery] int? referenceId,
            [FromQuery] int? warehouseId,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                return BadRequest(ApiResponse.BadRequest(message: "Payload quét QR không được để trống."));
            }

            try
            {
                var request = new QrResolveRequestDto
                {
                    Payload = payload,
                    Context = string.IsNullOrWhiteSpace(operation) ? null : new QrContextDto
                    {
                        Operation = operation,
                        ReferenceId = referenceId,
                        WarehouseId = warehouseId
                    }
                };
                var result = await _qrCodeService.ResolveQrAsync(request, cancellationToken);
                return Ok(ApiResponse.Success(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(ApiResponse.UnprocessableEntity(message: ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Error(message: ex.Message, status: 500, code: "CMN_500"));
            }
        }

        [HttpPost("qr/resolve")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> ResolveQrAsync([FromBody] QrResolveRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Payload))
            {
                return BadRequest(ApiResponse.BadRequest(message: "Payload quét QR không được để trống."));
            }

            try
            {
                var result = await _qrCodeService.ResolveQrAsync(request, cancellationToken);
                return Ok(ApiResponse.Success(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(ApiResponse.UnprocessableEntity(message: ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Error(message: ex.Message, status: 500, code: "CMN_500"));
            }
        }

        [HttpGet("qr-labels/preview")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> GetQrLabelPreviewAsync([FromQuery] string? labelType, [FromQuery] int? subjectId, [FromQuery] string? template, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _qrCodeService.GetQrLabelPreviewAsync(labelType, subjectId, template, cancellationToken);
                return Ok(ApiResponse.Success(result));
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (InvalidOperationException ex)
            {
                return UnprocessableEntity(ApiResponse.UnprocessableEntity(message: ex.Message));
            }
            catch (Exception ex)
            {
                return StatusCode(500, ApiResponse.Error(message: ex.Message, status: 500, code: "CMN_500"));
            }
        }

        [HttpGet("qr-labels/summary")]
        public async Task<IActionResult> GetQrLabelSummaryAsync(CancellationToken cancellationToken)
        {
            var result = await _qrCodeService.GetQrLabelSummaryAsync(cancellationToken);
            return Ok(ApiResponse.Success(result));
        }

        [HttpPost("qr-labels/history/paged")]
        public async Task<IActionResult> GetQrLabelHistoryAsync([FromBody] QrLabelHistoryQueryDto request, CancellationToken cancellationToken)
        {
            request ??= new QrLabelHistoryQueryDto();
            var result = await _qrCodeService.GetQrLabelHistoryAsync(request, cancellationToken);
            return Ok(ApiResponse.Success(result));
        }

        [HttpPost("qr-labels/paddy-lots/batch")]
        [CustomAuthorize(Enums.Menu.PADDY_LOTS, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintPaddyLotsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID lô hàng in nhãn không được rỗng."));
            }

            var formatUpper = request.Format?.ToUpper();
            if (formatUpper != "PDF" && formatUpper != "PNG")
            {
                return BadRequest(ApiResponse.BadRequest(message: "Định dạng xuất (Format) không hợp lệ. Chỉ chấp nhận PDF hoặc PNG."));
            }

            var validTemplates = new[] { "SMALL", "MEDIUM", "LARGE" };
            if (!validTemplates.Contains(request.Template?.ToUpper()))
            {
                return BadRequest(ApiResponse.BadRequest(message: "Kích cỡ nhãn (Template) không hợp lệ. Chỉ chấp nhận SMALL, MEDIUM hoặc LARGE."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            if (uniqueIds.Count * request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Tổng số lượng nhãn in (Số đối tượng × Số bản in) không được vượt quá 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (formatUpper == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkPaddyLotLabelsPngZipAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"paddy-lot-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkPaddyLotLabelsPdfAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"paddy-lot-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
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

        [HttpPost("qr-labels/locations/batch")]
        [CustomAuthorize(Enums.Menu.WAREHOUSES, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintLocationsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID vị trí in nhãn không được rỗng."));
            }

            var formatUpper = request.Format?.ToUpper();
            if (formatUpper != "PDF" && formatUpper != "PNG")
            {
                return BadRequest(ApiResponse.BadRequest(message: "Định dạng xuất (Format) không hợp lệ. Chỉ chấp nhận PDF hoặc PNG."));
            }

            var validTemplates = new[] { "SMALL", "MEDIUM", "LARGE" };
            if (!validTemplates.Contains(request.Template?.ToUpper()))
            {
                return BadRequest(ApiResponse.BadRequest(message: "Kích cỡ nhãn (Template) không hợp lệ. Chỉ chấp nhận SMALL, MEDIUM hoặc LARGE."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            if (uniqueIds.Count * request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Tổng số lượng nhãn in (Số đối tượng × Số bản in) không được vượt quá 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (formatUpper == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkLocationLabelsPngZipAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"location-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkLocationLabelsPdfAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"location-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
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

        [HttpPost("qr-labels/bags/batch")]
        [CustomAuthorize(Enums.Menu.PADDY_LOTS, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintBagsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID lô in nhãn bao không được rỗng."));
            }

            var formatUpper = request.Format?.ToUpper();
            if (formatUpper != "PDF" && formatUpper != "PNG")
            {
                return BadRequest(ApiResponse.BadRequest(message: "Định dạng xuất (Format) không hợp lệ. Chỉ chấp nhận PDF hoặc PNG."));
            }

            var validTemplates = new[] { "SMALL", "MEDIUM", "LARGE" };
            if (!validTemplates.Contains(request.Template?.ToUpper()))
            {
                return BadRequest(ApiResponse.BadRequest(message: "Kích cỡ nhãn (Template) không hợp lệ. Chỉ chấp nhận SMALL, MEDIUM hoặc LARGE."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            if (uniqueIds.Count * request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Tổng số lượng nhãn in (Số đối tượng × Số bản in) không được vượt quá 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (formatUpper == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkBagLabelsPngZipAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"bag-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkBagLabelsPdfAsync(uniqueIds, request.Template!, request.CopiesPerLabel, cancellationToken);
                    filename = $"bag-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
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
    }
}
