using System.Threading.Tasks;
using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Organizations;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Controller quản lý Doanh nghiệp/Tenant (Organization) - CRUD + tìm kiếm/lọc/sắp xếp.
    /// </summary>
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/organizations")]
    [Authorize]
    [ApiController]
    public class OrganizationController : BaseController
    {
        private readonly IOrganizationService _organizationService;

        public OrganizationController(IOrganizationService organizationService)
        {
            _organizationService = organizationService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _organizationService.GetAllAsync();
            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _organizationService.GetPagedAsync(parameters);
            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _organizationService.GetByIdAsync(id);
            return BaseResult(result);
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateOrganizationDto dto)
        {
            dto.CreatedBy = this.GetLoggedInUserId();
            var result = await _organizationService.CreateAsync(dto);
            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateOrganizationDto dto)
        {
            dto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _organizationService.UpdateAsync(dto);
            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _organizationService.SoftDeleteAsync(id);
            return BaseResult(result);
        }
    }
}
