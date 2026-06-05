using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.BlogPostLayouts;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/blog-layout")]
    [ApiController]
    public class BlogLayoutController : BaseController,
        IBaseController<int, CreateBlogPostLayoutDto, UpdateBlogPostLayoutDto, DTParameter>
    {
        private readonly IBlogLayoutService _blogLayoutService;
        public BlogLayoutController(IBlogLayoutService blogLayoutService)
        {
            _blogLayoutService = blogLayoutService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateBlogPostLayoutDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var data = await _blogLayoutService.CreateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var data = await _blogLayoutService.GetAllAsync();

            return BaseResult(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _blogLayoutService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _blogLayoutService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _blogLayoutService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var reuslt = await _blogLayoutService.SoftDeleteAsync(id);

            return BaseResult(reuslt);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateBlogPostLayoutDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var reuslt = await _blogLayoutService.UpdateAsync(obj);

            return BaseResult(reuslt);
        }

        [HttpPut("update-list")]
        public async Task<IActionResult> UpdateListAsync([FromBody] List<UpdateBlogPostLayoutDto> objs)
        {
            var result = await _blogLayoutService.UpdateListAsync(objs);

            return BaseResult(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] BlogPostLayoutSearchQuery query)
        {
            var data = await _blogLayoutService.GetPagedAsync(query);

            return BaseResult(data);
        }
    }
}
