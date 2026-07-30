using System.Threading.Tasks;
using Asp.Versioning;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    /// <summary>
    /// Tìm kiếm toàn cục cho thanh tìm kiếm trên header.
    /// Không gắn CustomAuthorize theo menu vì tìm xuyên nhiều màn; chỉ yêu cầu đã đăng nhập.
    /// Việc mở màn chi tiết vẫn bị chặn bởi RBAC ở route/API tương ứng.
    /// </summary>
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/search")]
    [ApiController]
    public class SearchController : BaseController
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        [HttpGet]
        public async Task<IActionResult> GlobalSearchAsync(
            [FromQuery] string? keyword,
            [FromQuery] int limit = 5)
        {
            var result = await _searchService.GlobalSearchAsync(keyword, limit);
            return BaseResult(result);
        }
    }
}
