using System;
using System.IdentityModel.Tokens.Jwt;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Enums;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Microsoft.EntityFrameworkCore;

namespace Backend.API.Middlewares;

public class TokenRevocationMiddleware
{
    private readonly RequestDelegate _next;

    public TokenRevocationMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, BackendContext dbContext, ITokenProviderService tokenProviderService)
    {
        if (!context.Request.Path.StartsWithSegments("/jobs"))
        {
            var accessToken = context.Request.Headers["Authorization"]
                .FirstOrDefault()?.Split(" ").Last();

            if (!string.IsNullOrEmpty(accessToken))
            {
                JwtSecurityToken? token = null;

                try
                {
                    token = tokenProviderService.ParseToken(accessToken);
                }
                catch
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    var apiResponse = ApiResponse.Unauthorized(
                        ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidToken), ApiCodeConstants.Auth.InvalidToken);
                    await context.Response.WriteAsJsonAsync(apiResponse);
                    return;
                }

                var jti = token?.Claims.FirstOrDefault(c => c.Type == ClaimNames.JTI)?.Value;
                var userId = token?.Claims.FirstOrDefault(c => c.Type == ClaimNames.ID)?.Value;

                if (!string.IsNullOrEmpty(jti))
                {
                    var isRevoked = await dbContext.UserSessions
                        .AnyAsync(r => r.AccessTokenJti == jti && r.IsRevoked);

                    if (isRevoked)
                    {
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        var apiResponse = ApiResponse.Unauthorized(
                            ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.AccessTokenRevoked),
                            ApiCodeConstants.Auth.AccessTokenRevoked);
                        await context.Response.WriteAsJsonAsync(apiResponse);
                        return;
                    }
                }

                if (!string.IsNullOrEmpty(userId))
                {
                    var userStatusId = await dbContext.Users
                        .Where(x => x.Id.ToString() == userId)
                        .Select(x => x.UserStatusId)
                        .FirstOrDefaultAsync();
                    if (userStatusId != (int)Enums.UserStatus.Actived)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        var apiResponse = ApiResponse.Forbidden(
                            ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden),
                            ApiCodeConstants.Common.Forbidden);
                        await context.Response.WriteAsJsonAsync(apiResponse);
                        return;
                    }

                }
            }
        }


        await _next(context);
    }
}
