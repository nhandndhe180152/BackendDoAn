using System;
using System.Transactions;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.DependencyInjection.Options;
using Backend.Infrastructure.Interceptors;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Repositories;
using Backend.Infrastructure.Services;
using Backend.Share.Entities;
using Backend.Share.Services;
using Hangfire;
using Hangfire.MySql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Backend.Infrastructure.DependencyInjection.Extentions;

public static class ConfigureServices
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BackendContext>((provider, options) =>
        {
            var configuration = provider.GetRequiredService<IConfiguration>();

            options.UseMySql(
                configuration.GetConnectionString("DefaultConnectionString"),
                ServerVersion.AutoDetect(configuration.GetConnectionString("DefaultConnectionString")),
                builder => builder.MigrationsAssembly(typeof(BackendContext).Assembly.FullName)
            );

            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));
        services.Configure<SmtpSettings>(configuration.GetSection("SmtpSettings"));
        services.Configure<StorageSettings>(configuration.GetSection("StorageSettings"));
        services.Configure<ScheduledJobConfig>(configuration.GetSection("ScheduledJobs"));
        services.Configure<CloudinarySettings>(configuration.GetSection("CloudinarySettings"));
        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddScoped<ITokenProviderService, TokenProviderService>();
        services.AddScoped<IStorageService, CloudinaryStorageService>();
        services.AddScoped(typeof(IRepositoryBaseDbContext<,,>), typeof(RepositoryBaseDbContext<,,>));
        services.AddScoped(typeof(IRepositoryBase<,>), typeof(RepositoryBase<,>));
        services.AddScoped(typeof(IUnitOfWork), typeof(UnitOfWork));
        services.AddScoped(typeof(IUnitOfWorkContext<>), typeof(UnitOfWorkContext<>));
        services.AddScoped<IActionRepository, ActionRepository>()
            .AddScoped<IActionInMenuRepository, ActionInMenuRepository>()
            .AddScoped<IActivityLogRepository, ActivityLogRepository>()
            .AddScoped<IBlogCategoryRepository, BlogCategoryRepository>()
            .AddScoped<IBlogCommentRepository, BlogCommentRepository>()
            .AddScoped<IBlogLayoutRepository, BlogLayoutRepository>()
            .AddScoped<IBlogPostRepository, BlogPostRepository>()
            .AddScoped<IBlogPostStatusRepository, BlogPostStatusRepository>()
            .AddScoped<IBlogTagRepository, BlogTagRepository>()
            .AddScoped<IFeedbackRepository, FeedbackRepository>()
            .AddScoped<IFileUploadRepository, FileUploadRepository>()
            .AddScoped<IFolderUploadRepository, FolderUploadRepository>()
            .AddScoped<IMenuRepository, MenuRepository>()
            .AddScoped<INotificationCategoryRepository, NotificationCategoryRepository>()
            .AddScoped<INotificationRepository, NotificationRepository>()
            .AddScoped<IPermissionRepository, PermissionRepository>()
            .AddScoped<IRoleRepository, RoleRepository>()
            .AddScoped<ISystemConfigRepository, SystemConfigRepository>()
            .AddScoped<ITagRepository, TagRepository>()
            .AddScoped<ITagTypeRepository, TagTypeRepository>()
            .AddScoped<IUserDeviceRepository, UserDeviceRepository>()
            .AddScoped<IUserNotificationRepository, UserNotificationRepository>()
            .AddScoped<IUserRepository, UserRepository>()
            .AddScoped<IUserRoleRepository, UserRoleRepository>()
            .AddScoped<IUserSessionRepository, UserSessionRepository>()
            .AddScoped<IUserStatusRepository, UserStatusRepository>()
            .AddScoped<IUserVerificationTokenRepository, UserVerificationTokenRepository>()
            .AddScoped<IAuditLogRepository, AuditLogRepository>()
            .AddScoped<INotificationTypeRepository, NotificationTypeRepository>()
            .AddScoped<IProductCategoryRepository, ProductCategoryRepository>()
            .AddScoped<IProductRepository, ProductRepository>()
            .AddScoped<IProductVariantRepository, ProductVariantRepository>()
            .AddScoped<IProductAttributeRepository, ProductAttributeRepository>()
            .AddScoped<IIotDeviceRepository, IotDeviceRepository>()
            .AddScoped<IIotWeightLogRepository, IotWeightLogRepository>()
            .AddScoped<IIotDeviceCommandRepository, IotDeviceCommandRepository>()
            .AddScoped<IWarehouseRepository, WarehouseRepository>()
            .AddScoped<ILocationRepository, LocationRepository>()
            .AddScoped<IInboundOrderItemRepository, InboundOrderItemRepository>()
            .AddScoped<IOutboundOrderItemRepository, OutboundOrderItemRepository>()
            .AddScoped<IStockTakeItemRepository, StockTakeItemRepository>()
            .AddScoped<IInventoryRepository, InventoryRepository>()
            .AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>();




        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();
        services.AddScoped<ISerializeService, SerializeService>();
        services.AddScoped<IScheduledJobService, ScheduledJobService>();
        services.AddScoped<IJobRegistrar, JobRegistrar>();
        services.AddScoped<UserSessionCleanupJob>();
        services.AddScoped<VerificationTokenCleanupJob>();
        services.AddScoped<IEmailService<GoogleMailRequest>, GoogleEmailService>();
        services.AddScoped<IImageProcessor, MagickImageProcessor>();
        services.AddScoped<IFireBaseService, FireBaseService>();

        services.AddHealthChecks()
            .AddCheck("self", () => HealthCheckResult.Healthy())
            .AddMySql(
                connectionString: configuration.GetConnectionString("DefaultConnectionString"),
                healthQuery: "SELECT 1;",
                name: "sql",
                failureStatus: HealthStatus.Unhealthy,
                tags: new[] { "db", "sql" }
            );

        var hangFireSettings = configuration.GetSection("HangfireSettings").Get<HangfireSettings>();

        services.AddHangfire(options =>
        {
            options.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UseStorage(new MySqlStorage(hangFireSettings?.ConnectionString,
                        new MySqlStorageOptions
                        {
                            TransactionIsolationLevel = IsolationLevel.ReadCommitted,
                            QueuePollInterval = TimeSpan.FromSeconds(15),
                            JobExpirationCheckInterval = TimeSpan.FromHours(1),
                            CountersAggregateInterval = TimeSpan.FromMinutes(5),
                            PrepareSchemaIfNecessary = true,
                            DashboardJobListLimit = 50000,
                            TransactionTimeout = TimeSpan.FromMinutes(1),
                            TablesPrefix = "Hangfire"
                        }
                ));
        });
        services.AddHangfireServer(options => options.ServerName = hangFireSettings?.ServerName);

        return services;
    }
}
