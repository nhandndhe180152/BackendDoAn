using System;
using System.Net;
using System.Threading.RateLimiting;
using Backend.Application.Constants;
using Backend.Share.Entities;
using Newtonsoft.Json;
using Backend.Application.DependencyInjection.Extentions;

namespace Backend.API.Utilities;

public static class RateLimitExtensions
{
    public static IServiceCollection AddRateLimitPolicies(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = (int)HttpStatusCode.TooManyRequests;

            options.OnRejected = async (context, token) =>
            {
                if (context.HttpContext.Request.Path.StartsWithSegments("/health"))
                {
                    context.HttpContext.Response.StatusCode = 200;
                    await context.HttpContext.Response.WriteAsync("Healthy", token);
                    return;
                }

                context.HttpContext.Response.ContentType = "application/json";

                var response = ApiResponse.Error(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.TooManyRequests), (int)HttpStatusCode.TooManyRequests, ApiCodeConstants.Common.TooManyRequests);

                context.HttpContext.Response.Headers["Retry-After"] = "60";

                var json = JsonConvert.SerializeObject(response);

                await context.HttpContext.Response.WriteAsync(json, token);
            };


            options.AddPolicy("StrictLoginPolicy", context =>
            {
                var ip = context.GetRemoteHostIpAddress()?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            options.AddPolicy("GeneralPolicy", context =>
            {
                var ip = context.GetRemoteHostIpAddress()?.ToString() ?? "unknown";
                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 100,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var ip = context.GetRemoteHostIpAddress()?.ToString() ?? "unknown";

                return RateLimitPartition.GetFixedWindowLimiter(ip, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 200,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
            });
        });

        return services;
    }
}
