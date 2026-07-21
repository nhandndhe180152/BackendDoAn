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

        [HttpPost("qr/resolve")]
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

        [HttpPost("qr-labels/paddy-lots/batch")]
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

            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 10)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 10."));
            }

            try
            {
                var bytes = await _qrCodeService.GenerateBulkPaddyLotLabelsPdfAsync(uniqueIds, request.Template, request.CopiesPerLabel, cancellationToken);
                var filename = $"paddy-lot-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";
                
                // Add filename to header for browser downloads
                Response.Headers.Add("Content-Disposition", $"attachment; filename={filename}");
                return File(bytes, "application/pdf", filename);
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

            if (request.CopiesPerLabel < 1 || request.CopiesPerLabel > 10)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số bản in trên mỗi nhãn (CopiesPerLabel) phải từ 1 đến 10."));
            }

            try
            {
                var bytes = await _qrCodeService.GenerateBulkLocationLabelsPdfAsync(uniqueIds, request.Template, request.CopiesPerLabel, cancellationToken);
                var filename = $"location-labels-{DateTime.UtcNow:yyyyMMdd}.pdf";

                Response.Headers.Add("Content-Disposition", $"attachment; filename={filename}");
                return File(bytes, "application/pdf", filename);
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
