using System;
using Backend.API.Filters;
using Backend.API.Middlewares;
using Backend.Infrastructure.DependencyInjection.Extentions;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Share.Entities;
using Hangfire;
using HealthChecks.UI.Client;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;

namespace Backend.API.Utilities;

public static class ApplicationExtensions
{
    public static void UseInfrastructure(this WebApplication app, IConfiguration configuration)
    {
        //if (app.Environment.IsDevelopment())
        //{
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            var descriptions = app.DescribeApiVersions();
            foreach (var description in descriptions)
            {
                options.SwaggerEndpoint($"/swagger/{description.GroupName}/swagger.json", description.GroupName.ToUpperInvariant());
            }
        });
        //}

        var allowedStaticPrefixes = new[] {
                "/uploads/users/avatars",
                "/uploads/blog-posts",
            };

        app.Use(async (context, next) =>
        {
            var requestPath = context.Request.Path.Value ?? "";

            if (requestPath.StartsWith("/uploads", StringComparison.OrdinalIgnoreCase) &&
                !allowedStaticPrefixes.Any(p => requestPath.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(ApiResponse.Forbidden());
                return;
            }
            await next();
        });

        app.UseStaticFiles();
        app.UseHttpsRedirection();
        app.UseCors("Default");
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();
        app.UseSerilogRequestLogging();
        app.UseMiddleware<TokenRevocationMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        var configDashboard = configuration.GetSection("HangfireSettings:Dashboard").Get<Dashboard>();
        var hangfireSettings = configuration.GetSection("HangfireSettings").Get<HangfireSettings>();
        var hangfireRoute = hangfireSettings?.Route;
        app.UseHangfireDashboard(hangfireRoute, new DashboardOptions
        {
            DashboardTitle = configDashboard?.DashboardTitle,
            StatsPollingInterval = configDashboard?.StatsPollingInterval ?? 200,
            AppPath = configDashboard?.AppPath,
            IgnoreAntiforgeryToken = true,
            Authorization = new[] { new BasicAuthDashboardAuthorizationFilter(configDashboard?.Username ?? string.Empty, configDashboard?.Password ?? string.Empty) }
        });
        app.RegisterJobs();
        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = r => r.Name == "self"
        });

        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = r => r.Name != "self",
            ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
        });
        app.Run();
    }
}
