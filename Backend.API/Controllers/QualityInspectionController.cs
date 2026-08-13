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
    }
}
