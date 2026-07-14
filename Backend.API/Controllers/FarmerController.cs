using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Farmers;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý danh mục Nông dân (Farmer) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/farmers")]
    [Authorize]
    [ApiController]
    public class FarmerController : BaseController
    {
        private readonly IFarmerService _farmerService;

        public FarmerController(IFarmerService farmerService)
        {
            _farmerService = farmerService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _farmerService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _farmerService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _farmerService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateFarmerDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _farmerService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateFarmerDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _farmerService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _farmerService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
