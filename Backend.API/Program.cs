using Backend.API.Utilities;
using Backend.Infrastructure.DependencyInjection.Extentions;
using Serilog;
using Serilog.Events;
using Backend.Application.DependencyInjection.Extentions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Information)
                .Enrich.FromLogContext()
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(le =>
                        le.Level == LogEventLevel.Information || le.Level == LogEventLevel.Warning)
                    .Filter.ByExcluding(le =>
                        le.Properties.ContainsKey("SourceContext") &&
                        le.Properties["SourceContext"].ToString().Contains("Microsoft.EntityFrameworkCore.Database.Command"))
                    .WriteTo.File("Logs/info-log.txt", rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"))
                .WriteTo.Logger(lc => lc
                    .Filter.ByIncludingOnly(le => le.Level >= LogEventLevel.Error)
                    .WriteTo.File("Logs/error-log.txt", rollingInterval: RollingInterval.Day,
                        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}"))
                .WriteTo.Console(outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}{NewLine}{Message:lj}{NewLine}{Exception}{NewLine}")

                .CreateLogger();
Log.Information("Start {ApplicationName} up.", builder.Environment.ApplicationName);

try
{
    // Add services to the container.
    builder.Services
        .AddInfrastructure(builder.Configuration)
        .AddApplicationServices(builder.Configuration)
        .AddInfrastructureServices(builder.Configuration);
    builder.Host.UseSerilog();
    builder.AddAppConfigurations();


    var app = builder.Build();

    // Configure the HTTP request pipeline.
    app.ApplyMigrations();
    await app.LoadCacheAsync();
    app.UseInfrastructure(builder.Configuration);
    
}
catch (Exception ex)
{
    string type = ex.GetType().Name;
    if (type.Equals("StopTheHostException", StringComparison.Ordinal)) throw;
    Log.Fatal(ex, "Unhanded exception: {Message}", ex.Message);
}
finally
{
    Log.Information("Shut down {ApplicationName} complete.", builder.Environment.ApplicationName);
    Log.CloseAndFlush();
}
