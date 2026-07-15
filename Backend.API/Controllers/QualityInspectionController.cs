using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _service.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _service.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _service.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpGet("by-lot/{paddyLotId}")]
        public async Task<IActionResult> GetByLotAsync(int paddyLotId)
        {
            var result = await _service.GetByLotAsync(paddyLotId);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateQualityInspectionDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _service.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateQualityInspectionDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _service.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _service.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
