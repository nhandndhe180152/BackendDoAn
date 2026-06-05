using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Actions;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/action")]
    [ApiController]
    public class ActionController : BaseController, IBaseController<int, CreateActionDto, UpdateActionDto, DTParameter>
    {
        private readonly IActionService _actionService;

        public ActionController(IActionService actionService)
        {
            _actionService = actionService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateActionDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _actionService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _actionService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _actionService.GetByIdAsync(id);

            return BaseResult(result);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _actionService.GetPagedAsync(query);

            return BaseResult(result);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var result = await _actionService.GetPagedAsync(parameters);

            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _actionService.SoftDeleteAsync(id);

            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateActionDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var result = await _actionService.UpdateAsync(obj);

            return BaseResult(result);
        }
    }
}
