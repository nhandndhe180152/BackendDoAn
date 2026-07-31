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
        public IActionResult GetQrLabelPreview()
        {
            var result = _qrCodeService.GetQrLabelPreviewSettings();
            return Ok(ApiResponse.Success(result));
        }

        [HttpPost("qr-labels/paddy-lots/batch")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintPaddyLotsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID lô hàng in nhãn không được rỗng."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (uniqueIds.Count > 200)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số lượng in nhãn hàng loạt tối đa là 200."));
            }

            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (request.Format?.ToUpper() == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkPaddyLotLabelsPngZipAsync(uniqueIds, cancellationToken);
                    filename = $"paddy-lot-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkPaddyLotLabelsPdfAsync(uniqueIds, request.Template, request.CopiesPerLabel, cancellationToken);
                    filename = $"paddy-lot-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
        }

        [HttpPost("qr-labels/locations/batch")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintLocationsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID vị trí in nhãn không được rỗng."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (uniqueIds.Count > 200)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số lượng in nhãn hàng loạt tối đa là 200."));
            }

            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (request.Format?.ToUpper() == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkLocationLabelsPngZipAsync(uniqueIds, cancellationToken);
                    filename = $"location-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkLocationLabelsPdfAsync(uniqueIds, request.Template, request.CopiesPerLabel, cancellationToken);
                    filename = $"location-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
        }

        [HttpPost("qr-labels/bags/batch")]
        [CustomAuthorize(Enums.Menu.PRODUCT_VARIANTS, Enums.Action.READ)]
        public async Task<IActionResult> BatchPrintBagsAsync([FromBody] BatchQrLabelPrintDto request, CancellationToken cancellationToken)
        {
            if (request == null || request.Ids == null || !request.Ids.Any())
            {
                return BadRequest(ApiResponse.BadRequest(message: "Danh sách ID lô in nhãn bao không được rỗng."));
            }

            var uniqueIds = request.Ids.Distinct().ToList();
            if (uniqueIds.Count > 200)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số lượng in nhãn hàng loạt tối đa là 200."));
            }

            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 500."));
            }

            try
            {
                byte[] bytes;
                string filename;
                if (request.Format?.ToUpper() == "PNG")
                {
                    bytes = await _qrCodeService.GenerateBulkBagLabelsPngZipAsync(uniqueIds, cancellationToken);
                    filename = $"bag-labels-{DateTime.UtcNow:yyyyMMdd}.zip";
                    return File(bytes, "application/zip", filename);
                }
                else
                {
                    bytes = await _qrCodeService.GenerateBulkBagLabelsPdfAsync(uniqueIds, request.Template, request.CopiesPerLabel, cancellationToken);
                    filename = $"bag-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                    return File(bytes, "application/pdf", filename);
                }
            }
            catch (KeyNotFoundException ex)
            {
                return NotFound(ApiResponse.NotFound(message: ex.Message));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
        }
    }
}
