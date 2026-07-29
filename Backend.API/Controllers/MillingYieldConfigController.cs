using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Backend.Domain.Enums;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Cấu hình tỷ lệ lúa→gạo (yield) theo giống lúa và dải độ ẩm (SCR-20). CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/milling-yield-configs")]
    [Authorize]
    [ApiController]
    public class MillingYieldConfigController : BaseController
    {
        private readonly IMillingYieldConfigService _millingYieldConfigService;

        public MillingYieldConfigController(IMillingYieldConfigService millingYieldConfigService)
        {
            _millingYieldConfigService = millingYieldConfigService;
        }

        [HttpGet]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _millingYieldConfigService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _millingYieldConfigService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _millingYieldConfigService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMillingYieldConfigDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _millingYieldConfigService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMillingYieldConfigDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _millingYieldConfigService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.MILLING_YIELD_CONFIGS, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _millingYieldConfigService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
