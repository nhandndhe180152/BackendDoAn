using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

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
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _riceVarietyService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _riceVarietyService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _riceVarietyService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRiceVarietyDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _riceVarietyService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateRiceVarietyDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _riceVarietyService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.RICE_VARIETIES, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _riceVarietyService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
