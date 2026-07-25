using System;
using System.Transactions;
using Backend.Application.Interfaces;
using Backend.Application.BackgroundJobs.LowStock;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
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
        var connectionString = configuration.GetConnectionString("DefaultConnectionString");
        var serverVersion = ServerVersion.AutoDetect(connectionString);

        services.AddDbContext<BackendContext>((provider, options) =>
        {
            options.UseMySql(
                connectionString,
                serverVersion,
                builder => builder.MigrationsAssembly(typeof(BackendContext).Assembly.FullName)
            );

            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<BackendContext>());

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
            .AddScoped<IFileUploadRepository, FileUploadRepository>()
            .AddScoped<IFolderUploadRepository, FolderUploadRepository>()
            .AddScoped<IMenuRepository, MenuRepository>()
            .AddScoped<INotificationCategoryRepository, NotificationCategoryRepository>()
            .AddScoped<INotificationRepository, NotificationRepository>()
            .AddScoped<IPermissionRepository, PermissionRepository>()
            .AddScoped<IRoleRepository, RoleRepository>()
            .AddScoped<ISystemConfigRepository, SystemConfigRepository>()
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
            .AddScoped<IWarehouseRepository, WarehouseRepository>()
            .AddScoped<ILocationRepository, LocationRepository>()
            .AddScoped<IInboundOrderItemRepository, InboundOrderItemRepository>()
            .AddScoped<IOutboundOrderItemRepository, OutboundOrderItemRepository>()
            .AddScoped<IStockTakeItemRepository, StockTakeItemRepository>()
            .AddScoped<IStockTakeRepository, StockTakeRepository>()
            .AddScoped<IInventoryRepository, InventoryRepository>()
            .AddScoped<IInventoryTransactionRepository, InventoryTransactionRepository>()
            .AddScoped<ISupplierRepository, SupplierRepository>()
            .AddScoped<IRiceVarietyRepository, RiceVarietyRepository>()
            .AddScoped<IFarmerRepository, FarmerRepository>()
            .AddScoped<ICustomerRepository, CustomerRepository>()
            .AddScoped<IOrganizationRepository, OrganizationRepository>()
            .AddScoped<IUnitOfMeasureRepository, UnitOfMeasureRepository>()
            // ── Rice supply chain repositories ───────────────────────────────────────
            .AddScoped<IPaddyLotRepository, PaddyLotRepository>()
            .AddScoped<IPaddyPurchaseReceiptRepository, PaddyPurchaseReceiptRepository>()
            .AddScoped<IPaddyPurchaseScheduleRepository, PaddyPurchaseScheduleRepository>()
            .AddScoped<IMillingOrderRepository, MillingOrderRepository>()
            .AddScoped<IPartyDebtRepository, PartyDebtRepository>()
            .AddScoped<IDebtTransactionRepository, DebtTransactionRepository>()
            .AddScoped<IQualityInspectionRepository, QualityInspectionRepository>()
            .AddScoped<IStockTransferRepository, StockTransferRepository>()
            .AddScoped<ISalesOrderRepository, SalesOrderRepository>()
            .AddScoped<IOutboundOrderRepository, OutboundOrderRepository>()
            // ── Cấu hình rule & cảnh báo (SCR-20/21) ─────────────────────────────────
            .AddScoped<IMillingYieldConfigRepository, MillingYieldConfigRepository>()
            .AddScoped<IStockAlertConfigRepository, StockAlertConfigRepository>()
            .AddScoped<IAlertRepository, AlertRepository>();




        services.AddMemoryCache();
        services.AddScoped<ICacheService, MemoryCacheService>();
        services.AddSingleton<ISystemLookup, SystemLookup>();
        services.AddScoped<ISerializeService, SerializeService>();
        services.AddScoped<IScheduledJobService, ScheduledJobService>();
        services.AddScoped<IJobRegistrar, JobRegistrar>();
        services.AddScoped<UserSessionCleanupJob>();
        services.AddScoped<VerificationTokenCleanupJob>();
        services.AddScoped<LowStockDetectionJob>();
        services.AddScoped<IntakeBottleneckEvaluationJob>();
        services.AddScoped<LotQualityRecheckJob>();
        services.AddScoped<DebtDueAndOverdueReminderJob>();
        services.AddScoped<FcmNotificationRetryJob>();
        services.AddScoped<ILowStockQueryService, LowStockQueryService>();
        services.AddScoped<ILowStockDetectionService, LowStockDetectionService>();
        services.AddScoped<IEmailService<GoogleMailRequest>, GoogleEmailService>();
        services.AddScoped<IImageProcessor, MagickImageProcessor>();
        services.AddScoped<IFireBaseService, FireBaseService>();
        services.AddScoped<IFcmClient, FcmClient>();
        services.AddScoped<IFcmFailureClassifier, FcmFailureClassifier>();
        services.AddScoped<IFcmNotificationRetryService, FcmNotificationRetryService>();

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
