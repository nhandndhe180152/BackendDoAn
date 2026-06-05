using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.BlogPostStatuses;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/blog-post-status")]
    [Authorize]
    [ApiController]
    public class BlogPostStatusController : BaseController,
        IBaseController<int, CreateBlogPostStatusDto, UpdateBlogPostStatusDto, DTParameter>
    {
        private readonly IBlogPostStatusService _blogPostStatusService;
        public BlogPostStatusController(IBlogPostStatusService blogPostStatusService)
        {
            _blogPostStatusService = blogPostStatusService;
        }
        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateBlogPostStatusDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var data = await _blogPostStatusService.CreateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var data = await _blogPostStatusService.GetAllAsync();

            return BaseResult(data);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _blogPostStatusService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _blogPostStatusService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _blogPostStatusService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var reuslt = await _blogPostStatusService.SoftDeleteAsync(id);

            return BaseResult(reuslt);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateBlogPostStatusDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var reuslt = await _blogPostStatusService.UpdateAsync(obj);

            return BaseResult(reuslt);
        }

        [HttpPut("update-list")]
        public async Task<IActionResult> UpdateListAsync([FromBody] List<UpdateBlogPostStatusDto> objs)
        {
            var result = await _blogPostStatusService.UpdateListAsync(objs);

            return BaseResult(result);
        }
    }
}
