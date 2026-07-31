using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.Constants;
using Backend.Application.DTOs.Locations;
using Backend.Application.DTOs.QrCode;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/location")]
    [ApiController]
    public class LocationController : BaseController
    {
        private readonly ILocationService _locationService;
        private readonly IQRCodeService _qrCodeService;
        private readonly IQrIdentifierService _qrIdentifierService;
        private readonly IApplicationDbContext _context;

        public LocationController(
            ILocationService locationService,
            IQRCodeService qrCodeService,
            IQrIdentifierService qrIdentifierService,
            IApplicationDbContext context)
        {
            _locationService = locationService;
            _qrCodeService = qrCodeService;
            _qrIdentifierService = qrIdentifierService;
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _locationService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _locationService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _locationService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateLocationDto dto)
        {
            var result = await _locationService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPost("list")]
        public async Task<IActionResult> CreateListAsync([FromBody] IEnumerable<CreateLocationDto> dtos)
        {
            var result = await _locationService.CreateListAsync(dtos);
            return BaseResult(result);
        }

        [HttpPut("list")]
        public async Task<IActionResult> UpdateListAsync([FromBody] IEnumerable<UpdateLocationDto> dtos)
        {
            var result = await _locationService.UpdateListAsync(dtos);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateLocationDto dto)
        {
            var result = await _locationService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _locationService.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        [HttpPost("{id}/qr/ensure")]
        public async Task<IActionResult> EnsureQrAsync(int id, CancellationToken cancellationToken)
        {
            try
            {
                var result = await _qrIdentifierService.EnsureLocationQrCodeAsync(id, cancellationToken);
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
                var result = await _qrIdentifierService.RegenerateLocationQrCodeAsync(id, dto.Reason, cancellationToken);
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
                var loc = await _context.Locations
                    .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);
                if (loc == null)
                    return NotFound(ApiResponse.NotFound(message: $"Không tìm thấy vị trí với ID {id}"));

                if (string.IsNullOrWhiteSpace(loc.QrImageUrl))
                    return NotFound(ApiResponse.NotFound(message: "Mã QR của vị trí này chưa được khởi tạo. Vui lòng gọi POST /{id}/qr/ensure trước."));

                return Ok(ApiResponse.Success(new
                {
                    entityId   = loc.Id,
                    qrCode     = loc.QrCode,
                    qrPayload  = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}",
                    qrImageUrl = loc.QrImageUrl
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
            if (copies < 1 || copies > 500)
            {
                return BadRequest(ApiResponse.BadRequest(message: "Số lượng bản in (copies) phải nằm trong khoảng từ 1 đến 500."));
            }

            try
            {
                var bytes = await _qrCodeService.GenerateLocationLabelPdfAsync(id, template, copies, cancellationToken);
                return File(bytes, "application/pdf", $"location-label-{id}.pdf");
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
