using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Tags;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/tag")]
    [Authorize]
    [ApiController]
    public class TagController : BaseController, IBaseController<int, CreateTagDto, UpdateTagDto, TagDTParamters>
    {
        private readonly ITagService _tagService;
        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }
        [HttpPost]
        [CustomAuthorize(Enums.Menu.TAG, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateTagDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _tagService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _tagService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.TAG, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _tagService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _tagService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.TAG, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] TagDTParamters parameters)
        {
            var data = await _tagService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.TAG, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _tagService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.TAG, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateTagDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _tagService.UpdateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] TagSearchQuery query)
        {
            var data = await _tagService.GetPagedAsync(query);

            return BaseResult(data);
        }
    }
}
