using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Wards;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/ward")]
    [ApiController]
    public class WardController : BaseController
    {
        private readonly IWardService _wardService;

        public WardController(IWardService wardService)
        {
            _wardService = wardService;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _wardService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _wardService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateWardDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _wardService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _wardService.GetByIdAsync(id);

            return BaseResult(result);
        }
        [HttpPut]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateWardDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var result = await _wardService.UpdateAsync(obj);

            return BaseResult(result);
        }
        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.PROVINCE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _wardService.SoftDeleteAsync(id);

            return BaseResult(result);
        }
    }
}
