using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Menus;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/menu")]
    [ApiController]
    public class MenuController : BaseController
    {
        private readonly IMenuService _menuService;

        public MenuController(IMenuService menuService)
        {
            _menuService = menuService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreateMenuDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _menuService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpPut]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateMenuDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var result = await _menuService.UpdateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _menuService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpPost("paged")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var result = await _menuService.GetPagedAsync(query);

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var result = await _menuService.GetByIdAsync(id);

            return BaseResult(result);
        }

        [HttpGet("permissons")]
        public async Task<IActionResult> GetMenuPermissonAsync()
        {
            var result = await _menuService.GetMenuPermissionAsync();

            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _menuService.SoftDeleteAsync(id);

            return BaseResult(result);
        }

        [HttpGet("types")]
        public async Task<IActionResult> GetAllMenuTypeAsync()
        {
            var result = await _menuService.GetAllMenuTypeAsync();

            return BaseResult(result);
        }
    }
}
