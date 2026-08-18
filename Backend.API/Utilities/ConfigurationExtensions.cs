using System;

namespace Backend.API.Utilities;

public static class ConfigurationExtensions
{
    public static void AddAppConfigurations(this WebApplicationBuilder builder)
    {
        var env = builder.Environment;
        builder.Configuration
                // Local dev: đọc file trong thư mục app (máy bạn có sẵn appsettings.json)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true)
                // Render (Docker): Secret File được mount ở /etc/secrets/<tên-file>
                .AddJsonFile("/etc/secrets/appsettings.json", optional: true, reloadOnChange: true)
                // Env var override mọi thứ ở trên (tùy chọn)
                .AddEnvironmentVariables();
    }
}
