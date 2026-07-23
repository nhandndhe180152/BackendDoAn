using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.DTOs.QrCode;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Lô lúa/gạo (PaddyLot) — truy vết lô.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/paddy-lots")]
    [Authorize]
    [ApiController]
    public class PaddyLotController : BaseController
    {
        private readonly IPaddyLotService _paddyLotService;
        private readonly IPaddyLotTraceabilityService _traceabilityService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IQrIdentifierService _qrIdentifierService;
        private readonly IApplicationDbContext _context;

        public PaddyLotController(
            IPaddyLotService paddyLotService,
            IPaddyLotTraceabilityService traceabilityService,
            IQRCodeService qrCodeService,
            IQrIdentifierService qrIdentifierService,
            IApplicationDbContext context)
        {
            _paddyLotService = paddyLotService;
            _traceabilityService = traceabilityService;
            _qrCodeService = qrCodeService;
            _qrIdentifierService = qrIdentifierService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _paddyLotService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _paddyLotService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _paddyLotService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePaddyLotDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _paddyLotService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdatePaddyLotDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _paddyLotService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _paddyLotService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPost("{id}/qr/ensure")]
        public async Task<IActionResult> EnsureQrAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _qrIdentifierService.EnsurePaddyLotQrCodeAsync(id, cancellationToken);
                return Ok(ApiResponse.Success(result));
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

        [HttpPost("{id}/qr/regenerate")]
        public async Task<IActionResult> RegenerateQrAsync(int id, [FromBody] RegenerateQrRequestDto dto, CancellationToken cancellationToken)
        {
            var userId = this.GetLoggedInUserId();
            var isAdmin = await _context.UserRoles
                .AnyAsync(ur => !ur.IsDeleted && ur.UserId == userId &&
                    _context.Roles.Any(r => r.Id == ur.RoleId && !r.IsDeleted && r.Code == "ADMIN"),
                    cancellationToken);
            if (!isAdmin)
            {
                return BaseResult(ApiResponse.Forbidden(message: "Chỉ quản trị viên mới được phép làm mới mã QR.", code: ApiCodeConstants.Qr.RegenerateForbidden));
            }

            try
            {
                var result = await _qrIdentifierService.RegeneratePaddyLotQrCodeAsync(id, dto.Reason, cancellationToken);
                return Ok(ApiResponse.Success(result));
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

        [HttpGet("{id}/qr/image")]
        public async Task<IActionResult> GetQrImageAsync(int id, CancellationToken cancellationToken = default)
        {
            try
            {
                var lot = await _context.PaddyLots
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
                if (lot == null)
                    return NotFound(ApiResponse.NotFound(message: $"Không tìm thấy lô hàng với ID {id}"));

                if (string.IsNullOrWhiteSpace(lot.QrImageUrl))
                    return NotFound(ApiResponse.NotFound(message: "Mã QR của lô hàng này chưa được khởi tạo. Vui lòng gọi POST /{id}/qr/ensure trước."));

                return Ok(ApiResponse.Success(new
                {
                    entityId  = lot.Id,
                    qrCode    = lot.QrCode,
                    qrPayload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}",
                    qrImageUrl = lot.QrImageUrl
                }));
            }
            catch (Exception ex)
            {
                return BadRequest(ApiResponse.BadRequest(message: ex.Message));
            }
        }

        [HttpGet("{id}/label")]
        public async Task<IActionResult> GetLabelPdfAsync(int id, [FromQuery] string template = "MEDIUM", [FromQuery] int copies = 1, CancellationToken cancellationToken = default)
        {
            if (copies < 1 || copies > 10)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số lượng bản in (copies) phải nằm trong khoảng từ 1 đến 10."));
            }

            try
            {
                var bytes = await _qrCodeService.GeneratePaddyLotLabelPdfAsync(id, template, copies, cancellationToken);
                return File(bytes, "application/pdf", $"paddylot-label-{id}.pdf");
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

        [HttpGet("{id:int}/traceability")]
        public async Task<IActionResult> GetTraceabilityByIdAsync(
            int id,
            [FromQuery] bool includeTimeline = true,
            [FromQuery] bool includeQuality = true,
            [FromQuery] bool includeMilling = true,
            [FromQuery] bool includeOutbound = true,
            [FromQuery] int maxDepth = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _traceabilityService.GetByLotIdAsync(
                id,
                includeTimeline,
                includeQuality,
                includeMilling,
                includeOutbound,
                maxDepth,
                cancellationToken);

            return BaseResult(result);
        }

        [HttpGet("code/{lotCode}/traceability")]
        public async Task<IActionResult> GetTraceabilityByCodeAsync(
            string lotCode,
            [FromQuery] bool includeTimeline = true,
            [FromQuery] bool includeQuality = true,
            [FromQuery] bool includeMilling = true,
            [FromQuery] bool includeOutbound = true,
            [FromQuery] int maxDepth = 10,
            CancellationToken cancellationToken = default)
        {
            var result = await _traceabilityService.GetByLotCodeAsync(
                lotCode,
                includeTimeline,
                includeQuality,
                includeMilling,
                includeOutbound,
                maxDepth,
                cancellationToken);

            return BaseResult(result);
        }
    }
}
