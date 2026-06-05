using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.TagTypes;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/tag-type")]
    [Authorize]
    [ApiController]
    public class TagTypeController : BaseController, IBaseController<int, CreateTagTypeDto, UpdateTagTypeDto, DTParameter>
    {
        private readonly ITagTypeService _tagTypeService;

        public TagTypeController(ITagTypeService tagTypeService)
        {
            _tagTypeService = tagTypeService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateTagTypeDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _tagTypeService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _tagTypeService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _tagTypeService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _tagTypeService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _tagTypeService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _tagTypeService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateTagTypeDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _tagTypeService.UpdateAsync(obj);

            return BaseResult(data);
        }
    }
}
