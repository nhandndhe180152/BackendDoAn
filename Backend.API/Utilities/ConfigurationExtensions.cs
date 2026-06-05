using System;

namespace Backend.API.Utilities;

public static class ConfigurationExtensions
{
    public static void AddAppConfigurations(this WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
    }
}
