using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.BlogPosts;
using Backend.Application.Interfaces;
using Backend.Domain.DTParameters;
using Backend.Domain.Enums;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/blog-post")]
    [ApiController]
    public class BlogPostController : BaseController, IBaseController<int, CreateBlogPostDto, UpdateBlogPostDto, BlogPostDTParameters>
    {
        private readonly IBlogPostService _blogPostService;
        public BlogPostController(IBlogPostService blogPostService)
        {
            _blogPostService = blogPostService;
        }
        [HttpPost]
        [CustomAuthorize(Enums.Menu.BLOG_POST_LIST, Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateBlogPostDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _blogPostService.CreateAsync(obj);
            return BaseResult(result);
        }
        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _blogPostService.GetAllAsync();
            return BaseResult(result);
        }
        [HttpGet("{id}")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_LIST, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _blogPostService.GetByIdAsync(id);
            return BaseResult(data);
        }
        [HttpPost("paged")]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _blogPostService.GetPagedAsync(query);
            return BaseResult(data);
        }

        [HttpPost("paged-client")]
        public async Task<IActionResult> GetPagedAsync([FromBody] BlogPostSearchDTParameters parameters)
        {
            var data = await _blogPostService.GetPagedClientAsync(parameters);
            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_LIST, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] BlogPostDTParameters parameters)
        {
            var data = await _blogPostService.GetPagedAsync(parameters);
            return BaseResult(data);
        }
        [HttpDelete("{id}")]
        [CustomAuthorize(Enums.Menu.BLOG_POST_LIST, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _blogPostService.SoftDeleteAsync(id);

            return BaseResult(data);
        }
        [HttpPut]
        [CustomAuthorize(Enums.Menu.BLOG_POST_LIST, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateBlogPostDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _blogPostService.UpdateAsync(obj);

            return BaseResult(data);
        }
        [HttpGet("popular")]
        [AllowAnonymous]
        public async Task<IActionResult> GetLatestArticleList()
        {
            var result = await _blogPostService.GetLatestArticleList();

            return BaseResult(result);
        }
    }
}
