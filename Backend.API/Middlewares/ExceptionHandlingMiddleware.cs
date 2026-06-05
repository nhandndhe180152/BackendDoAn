using System;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Application.DependencyInjection.Extentions;

namespace Backend.API.Middlewares;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, BackendContext dbContext, ITokenProviderService tokenProviderService)
    {
        if (context.Request.Path.StartsWithSegments("/jobs"))
            await _next(context);
        else
        {
            var request = context.Request;
            var ipAddress = context.GetRemoteHostIpAddress();
            var userAgent = request.Headers["User-Agent"].ToString();
            var targetType = request.RouteValues["controller"]?.ToString();
            int userId = 0;
            var accessToken = context.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            JwtSecurityToken? token = null;

            int statusCode = 200;
            string errorMessage = string.Empty;
            try
            {
                if (!string.IsNullOrEmpty(accessToken))
                {
                    token = tokenProviderService.ParseToken(accessToken);
                    userId = Convert.ToInt32(token?.Claims.FirstOrDefault(c => c.Type == ClaimNames.ID)?.Value);
                }

                await _next(context);
                statusCode = context.Response.StatusCode;
            }
            catch (Exception exception)
            {
                statusCode = 500;
                errorMessage = exception.Message;

                _logger.LogError(
                    exception, "Exception occurred: {Message}", exception.Message);
                var apiCode = ApiCodeConstants.Common.InternalServerError;
                var apiResponse = new ApiResponse
                {
                    Code = apiCode,
                    Errors = null,
                    IsSucceeded = false,
                    Message = ErrorMessagesConstants.GetMessage(apiCode),
                    Resources = null,
                    Status = (int)HttpStatusCode.InternalServerError
                };

                context.Response.StatusCode =
                    StatusCodes.Status500InternalServerError;

                await context.Response.WriteAsJsonAsync(apiResponse);
            }
            finally
            {
                try
                {
                    //save log to DB
                    var log = new ActivityLog
                    {
                        Action = $"{request.Method} {request.Path}",
                        Description = statusCode < 300 ? $"{statusCode} OK" : $"{statusCode} ERR {errorMessage}",
                        IpAddress = ipAddress,
                        UserAgent = userAgent,
                        CreatedDate = DateTime.Now,
                        CreatedBy = userId == 0 ? null : userId,
                        ActivityLogType = CommonConstants.ActivityLogType.REQUEST,
                        TargetType = targetType ?? string.Empty
                    };


                    //dbContext.ActivityLogs.AddAsync(log);
                    //dbContext.SaveChangesAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save log with message {Message}", ex.Message);
                }

            }
        }

    }
}
