using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý danh mục Giống lúa (RiceVariety) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/rice-varieties")]
    [Authorize]
    [ApiController]
    public class RiceVarietyController : BaseController
    {
        private readonly IRiceVarietyService _riceVarietyService;

        public RiceVarietyController(IRiceVarietyService riceVarietyService)
        {
            _riceVarietyService = riceVarietyService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _riceVarietyService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _riceVarietyService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _riceVarietyService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRiceVarietyDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _riceVarietyService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateRiceVarietyDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _riceVarietyService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _riceVarietyService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
