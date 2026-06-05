using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.SystemConfigs;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/system-config")]
    [Authorize]
    [ApiController]
    public class SystemConfigController : BaseController, IBaseController<int, CreateSystemConfigDto, UpdateSystemConfigDto, DTParameter>
    {
        private readonly ISystemConfigService _systemConfigService;

        public SystemConfigController(ISystemConfigService systemConfigService)
        {
            _systemConfigService = systemConfigService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateSystemConfigDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _systemConfigService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _systemConfigService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _systemConfigService.GetByIdAsync(id);

            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _systemConfigService.GetPagedAsync(query);

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _systemConfigService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _systemConfigService.SoftDeleteAsync(id);

            return BaseResult(result);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateSystemConfigDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var result = await _systemConfigService.UpdateAsync(obj);

            return BaseResult(result);
        }

        [HttpPut("update-list")]
        [CustomAuthorize(Enums.Menu.SYSTEM_CONFIG, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateListAsync([FromBody] List<UpdateSystemConfigDto> objs)
        {
            var result = await _systemConfigService.UpdateListAsync(objs);

            return BaseResult(result);
        }

        [HttpGet("contact-information")]
        [AllowAnonymous]
        public async Task<IActionResult> GetContactInformationAsync()
        {
            var result = await _systemConfigService.GetContactInformationAsync();
            return BaseResult(result);
        }

        [HttpGet("privacy-policy")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPrivacyPolicyAsync()
        {
            var result = await _systemConfigService.GetPrivacyPolicyAsync();
            return BaseResult(result);
        }

        [HttpGet("term-of-service")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTermOfServiceAsync()
        {
            var result = await _systemConfigService.GetTermOfServiceAsync();
            return BaseResult(result);
        }
    }
}
