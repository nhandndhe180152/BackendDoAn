using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

        public PaddyLotController(IPaddyLotService paddyLotService)
        {
            _paddyLotService = paddyLotService;
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
    }
}
