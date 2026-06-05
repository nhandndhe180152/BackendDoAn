using System;
using Backend.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Backend.Infrastructure.DependencyInjection.Extentions;

public static class JobInitializer
{
    public static void RegisterJobs(this IHost host)
    {
        using (var scope = host.Services.CreateScope())
        {
            var registrar = scope.ServiceProvider.GetRequiredService<IJobRegistrar>();
            registrar.RegisterJobs();
        }
    }
}
