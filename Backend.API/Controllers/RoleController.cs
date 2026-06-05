using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.Permissions;
using Backend.Application.DTOs.Roles;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/role")]
    [Authorize]
    [ApiController]
    public class RoleController : BaseController, IBaseController<int, CreateRoleDto, UpdateRoleDto, DTParameter>
    {
        private readonly IRoleService _roleService;

        public RoleController(IRoleService roleService)
        {
            _roleService = roleService;
        }

        [HttpPost]
        //[CustomAuthorize(Enums.Menu.ROLE,Enums.Action.CREATE)]
        public async Task<IActionResult> CreateAsync([FromBody] CreateRoleDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            var result = await _roleService.CreateAsync(obj);

            return BaseResult(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync()
        {
            var result = await _roleService.GetAllAsync();

            return BaseResult(result);
        }

        [HttpGet("{id}")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.READ)]
        public async Task<IActionResult> GetByIdAsync(int id)
        {
            var data = await _roleService.GetByIdAsync(id);

            return BaseResult(data);
        }

        [HttpPost("paged")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] SearchQuery query)
        {
            var data = await _roleService.GetPagedAsync(query);

            return BaseResult(data);
        }

        [HttpPost("paged-advanced")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.READ)]
        public async Task<IActionResult> GetPagedAsync([FromBody] DTParameter parameters)
        {
            var data = await _roleService.GetPagedAsync(parameters);

            return BaseResult(data);
        }

        [HttpDelete("{id}")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.DELETE)]
        public async Task<IActionResult> SoftDeleteAsync(int id)
        {
            var data = await _roleService.SoftDeleteAsync(id);

            return BaseResult(data);
        }

        [HttpPut]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdateAsync([FromBody] UpdateRoleDto obj)
        {
            obj.UpdatedBy = this.GetLoggedInUserId();
            var data = await _roleService.UpdateAsync(obj);

            return BaseResult(data);
        }

        [HttpGet("{roleId}/permissons")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.READ)]
        public async Task<IActionResult> GetPermissionAsync(int roleId)
        {
            var result = await _roleService.GetPermissionAsync(roleId);

            return BaseResult(result);
        }

        [HttpPut("{roleId}/permissons")]
        //[CustomAuthorize(Enums.Menu.ROLE, Enums.Action.UPDATE)]
        public async Task<IActionResult> UpdatePermissionAsync(int roleId, [FromBody] List<UpdatePermissionDto> objs)
        {
            var userId = this.GetLoggedInUserId();
            objs.ForEach(x =>
            {
                x.UpdatedBy = userId;
                x.RoleId = roleId;
            });
            var result = await _roleService.UpdatePermissionAsync(roleId, objs);

            return BaseResult(result);
        }

        [AllowAnonymous]
        [HttpGet("available-for-register")]
        public async Task<IActionResult> GetByAdminRegister()
        {
            var result = await _roleService.GetByAdminRegister();
            return BaseResult(result);
        }

        [HttpGet("available-for-user-management")]
        public async Task<IActionResult> ListRoleForUserManagementAsync()
        {
            var result = await _roleService.GetListRoleForUserManagementAsync();
            return BaseResult(result);
        }
    }
}
