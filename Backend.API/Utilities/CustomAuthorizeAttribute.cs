using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Backend.Application.Constants;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Share.Entities;
using Backend.Share.Services;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;

namespace Backend.API.Utilities;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class CustomAuthorizeAttribute : Attribute, IAsyncAuthorizationFilter
{
    private readonly int _actionId;
    private readonly int _menuId;
    public CustomAuthorizeAttribute(Enums.Menu menuId, Enums.Action actionId)
    {
        _actionId = (int)actionId;
        _menuId = (int)menuId;
    }
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var accessToken = context.HttpContext.Request.Headers["Authorization"]
            .FirstOrDefault()?.Split(" ").Last();
        if (!string.IsNullOrEmpty(accessToken))
        {
            JwtSecurityToken? token = null;
            //var tokenProviderService = context.HttpContext.RequestServices.GetService<ITokenProviderService>();
            var cacheService = context.HttpContext.RequestServices.GetService<ICacheService>();
            var dbContext = context.HttpContext.RequestServices.GetService<BackendContext>();
            try
            {
                //token = tokenProviderService?.ParseToken(accessToken);
                //var userId = token?.Claims.FirstOrDefault(c => c.Type == ClaimNames.ID)?.Value;
                //var officeId=token?.Claims.FirstOrDefault(c=>c.Type==ClaimNames.OFFICE_ID)?.Value;
                var userId = context.HttpContext.GetCurrentUserId();

                //get role
                var roleIds = await dbContext?.UserRoles
                    .Where(x => !x.IsDeleted && x.UserId == userId)
                    .Select(x => x.RoleId)
                    .ToListAsync();
                //get list permissions from cache service
                var listPermissions = await cacheService?.GetAsync<List<Permission>>(CommonConstants.Cache.PERMISSIONS_ALL_KEY);
                if (listPermissions == null || !listPermissions.Any())
                    listPermissions = await dbContext.Permissions
                        .Where(x => !x.IsDeleted && roleIds.Contains(x.RoleId))
                        .ToListAsync();
                if (listPermissions.Any())
                {
                    //check permisson
                    var isAllowed = listPermissions
                        .Any(x => x.ActionId == _actionId && x.MenuId == _menuId &&
                            roleIds.Contains(x.RoleId));
                    if (!isAllowed)
                    {
                        var response = ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
                        context.Result = new ObjectResult(response)
                        {
                            StatusCode = (int)HttpStatusCode.Forbidden
                        };
                    }

                }
                else
                {
                    var response = ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);
                    context.Result = new ObjectResult(response)
                    {
                        StatusCode = (int)HttpStatusCode.Forbidden
                    };
                }
            }
            catch
            {
                var response = ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidToken), ApiCodeConstants.Auth.InvalidToken);
                context.Result = new ObjectResult(response)
                {
                    StatusCode = (int)HttpStatusCode.Unauthorized
                };
            }
        }
        else
        {
            var response = ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Unauthorized), ApiCodeConstants.Common.Unauthorized);
            context.Result = new ObjectResult(response)
            {
                StatusCode = (int)HttpStatusCode.Unauthorized
            };
        }
    }
}
