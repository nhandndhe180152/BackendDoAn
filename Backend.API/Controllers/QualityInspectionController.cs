using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Phiếu kiểm tra chất lượng lô lúa/gạo (QualityInspection).
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/quality-inspections")]
    [Authorize]
    [ApiController]
    public class QualityInspectionController : BaseController
    {
        private readonly IQualityInspectionService _service;

        public QualityInspectionController(IQualityInspectionService service)
        {
            _service = service;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _service.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _service.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("by-lot/{paddyLotId}")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.READ)]
        public async Task<IActionResult> GetByLotAsync(int paddyLotId)
        {
            var result = await _service.GetByLotAsync(paddyLotId);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateQualityInspectionDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            dto.InspectorId = dto.CreatedBy;
            var result = await _service.CreateAsync(dto);
            return BaseResult(result);
        }

        /// <summary>Kiểm tra lại chất lượng lô đang CÁCH LY; nếu đạt sẽ rút hàng khỏi ô cách ly và tạo phiếu nhập kho để xếp lại vào ô thường.</summary>
        [HttpPost("recheck")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.APPROVE)]
        public async Task<IActionResult> RecheckAsync([FromBody] CreateQualityInspectionDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            dto.InspectorId = dto.CreatedBy;
            var result = await _service.RecheckAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateQualityInspectionDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            dto.InspectorId = dto.UpdatedBy;
            var result = await _service.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _service.SoftDeleteAsync(id);
            return BaseResult(result);
        }

        // ── W14-D: Bag-level ─────────────────────────────────────────────────

        /// <summary>GET tiến độ kiểm tra bag-level của một inspection session.</summary>
        [HttpGet("{inspectionId}/bags")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.READ)]
        public async Task<IActionResult> GetBagProgressAsync(int inspectionId)
        {
            var result = await _service.GetBagProgressAsync(inspectionId);
            return BaseResult(result);
        }

        /// <summary>
        /// PUT autosave kết quả kiểm tra cho 1 bao (upsert idempotent).
        /// Chỉ lưu QC result — không tác động inventory/lot/debt.
        /// </summary>
        [HttpPut("{inspectionId}/bags/{bagId}")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.UPDATE)]
        public async Task<IActionResult> SaveBagResultAsync(int inspectionId, int bagId, [FromBody] SaveBagInspectionResultDto dto)
        {
            dto.BagId = bagId;
            dto.InspectorId = this.GetLoggedInUserId();
            var result = await _service.SaveBagResultAsync(inspectionId, dto);
            return BaseResult(result);
        }

        // ── W14-E: Complete ───────────────────────────────────────────────────

        /// <summary>
        /// Hoàn tất inspection session.
        /// Validate 100% bag đã kiểm tra → finalize → aggregate header → mark CompletedAt.
        /// Idempotent: gọi lại khi đã complete sẽ trả 409.
        /// </summary>
        [HttpPost("{inspectionId}/complete")]
        [CustomAuthorize(Enums.Menu.QUALITY_INSPECTIONS, Enums.Action.APPROVE)]
        public async Task<IActionResult> CompleteAsync(int inspectionId, [FromBody] CompleteInspectionDto dto)
        {
            dto.CompletedBy = this.GetLoggedInUserId();
            var result = await _service.CompleteAsync(inspectionId, dto);
            return BaseResult(result);
        }
        // ── W14-J: Moisture Config ────────────────────────────────────────────

        /// <summary>
        /// Trả về ngưỡng độ ẩm nhập kho từ SystemConfig — FE/mobile dùng để hiển thị cảnh báo, không hard-code.
        /// Keys: ReceivingQcMoistureMinPercent, ReceivingQcMoistureMaxPercent, StorageQcMoistureWarningPercent.
        /// </summary>
        [HttpGet("config")]
        [AllowAnonymous]
        public async Task<IActionResult> GetMoistureConfigAsync()
        {
            var result = await _service.GetMoistureConfigAsync();
            return BaseResult(result);
        }
    }
}
