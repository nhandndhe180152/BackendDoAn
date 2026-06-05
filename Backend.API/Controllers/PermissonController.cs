using Asp.Versioning;
using Backend.Application.DTOs.Permissions;
using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/permisson")]
    [ApiController]
    public class PermissonController : BaseController
    {
        private readonly IPermissionService _permissionService;

        public PermissonController(IPermissionService permissionService)
        {
            _permissionService = permissionService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateAsync([FromBody] CreatePermissionDto obj)
        {
            var result = await _permissionService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var result = await _permissionService.SoftDeleteAsync(id);

            return BaseResult(result);
        }
    }
}
