using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Provinces;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/province")]
    [ApiController]
    public class ProvinceController : BaseController
    {
        private readonly IProvinceService _provinceService;

        public ProvinceController(IProvinceService provinceService)
        {
            _provinceService = provinceService;
        }

        [Authorize]
        [HttpPost("sync-data")]
        public async Task<IActionResult> SyncDataAsync()
        {
            var result = await _provinceService.SyncDataAsync();

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _provinceService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{provinceId}/wards")]
        public async Task<IActionResult> GetWardsAsync(int provinceId)
        {
            var result = await _provinceService.GetWardsAsync(provinceId);

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _provinceService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [Authorize]
        [HttpPost]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateProvinceDto createProvinceDto)
        {
            createProvinceDto.CreatedBy = this.GetLoggedInUserId();
            var result = await _provinceService.CreateAsync(createProvinceDto);

            return BaseResult(result);
        }

        [Authorize]
        [HttpPut]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateProvinceDto updateProvinceDto)
        {
            updateProvinceDto.UpdatedBy = this.GetLoggedInUserId();
            var result = await _provinceService.UpdateAsync(updateProvinceDto);

            return BaseResult(result);
        }

        [Authorize]
        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _provinceService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [Authorize]
        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _provinceService.GetByIdAsync(id);

            return BaseResult(data);
        }
    }
}
