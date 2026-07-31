using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Đơn vị tính (UnitOfMeasure) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// Bảng phẳng nên dùng DTParameter chung.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/unit-of-measures")]
    [Authorize]
    [ApiController]
    public class UnitOfMeasureController : BaseController
    {
        private readonly IUnitOfMeasureService _unitOfMeasureService;

        public UnitOfMeasureController(IUnitOfMeasureService unitOfMeasureService)
        {
            _unitOfMeasureService = unitOfMeasureService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _unitOfMeasureService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _unitOfMeasureService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _unitOfMeasureService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateUnitOfMeasureDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _unitOfMeasureService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateUnitOfMeasureDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _unitOfMeasureService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.UNIT_OF_MEASURES, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _unitOfMeasureService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
