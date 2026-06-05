using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.BlogPostCategories;
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
    [Route("api/v{version:apiVersion}/blog-category")]
    [ApiController]
    public class BlogCategoryController : BaseController,
        IBaseController<int, CreateBlogPostCategoryDto, UpdateBlogPostCategoryDto, DTParameter>
    {
        private readonly IBlogCategoryService _blogCategoryService;
        public BlogCategoryController(IBlogCategoryService blogCategoryService)
        {
            _blogCategoryService = blogCategoryService;
        }

        [HttpPost]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateBlogPostCategoryDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var data = await _blogCategoryService.CreateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var data = await _blogCategoryService.GetAllAsync();

            return BaseResult(data);
        }

        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _blogCategoryService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _blogCategoryService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _blogCategoryService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var reuslt = await _blogCategoryService.SoftDeleteAsync(id);

            return BaseResult(reuslt);
        }

        [HttpPut]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateBlogPostCategoryDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var reuslt = await _blogCategoryService.UpdateAsync(obj);

            return BaseResult(reuslt);
        }

        [HttpPut("update-list")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_CATEGORY, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateListAsync([FromBody] List<UpdateBlogPostCategoryDto> objs)
        {
            var result = await _blogCategoryService.UpdateListAsync(objs);

            return BaseResult(result);
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] BlogPostCategorySearchQuery query)
        {
            var data = await _blogCategoryService.GetPagedAsync(query);

            return BaseResult(data);
        }
    }
}
