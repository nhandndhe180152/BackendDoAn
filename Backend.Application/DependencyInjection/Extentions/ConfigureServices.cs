using System;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Application.Validators.Auths;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Backend.Application.DependencyInjection.Extentions;

public static class ConfigureServices
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IActionService, ActionService>()
            .AddScoped<IActivityLogService, ActivityLogService>()
            .AddScoped<IFileUploadService, FileUploadService>()
            .AddScoped<IFolderUploadService, FolderUploadService>()
            .AddScoped<IMenuService, MenuService>()
            .AddScoped<INotificationCategoryService, NotificationCategoryService>()
            .AddScoped<INotificationService, NotificationService>()
            .AddScoped<IPermissionService, PermissionService>()
            .AddScoped<IRoleService, RoleService>()
            .AddScoped<ISystemConfigService, SystemConfigService>()
            .AddScoped<IUserDeviceService, UserDeviceService>()
            .AddScoped<IUserNotificationService, UserNotificationService>()
            .AddScoped<IUserService, UserService>()
            .AddScoped<IUserRoleService, UserRoleService>()
            .AddScoped<IUserSessionService, UserSessionService>()
            .AddScoped<IUserStatusService, UserStatusService>()
            .AddScoped<IUserVerificationTokenService, UserVerificationTokenService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IEmailTemplateService, EmailTemplateService>()
            .AddScoped<IAuditLogService, AuditLogService>()
            .AddScoped<INotificationTypeService, NotificationTypeService>()
            .AddScoped<IDashboardService, DashboardService>()
            .AddScoped<IProductCategoryService, ProductCategoryService>()
            .AddScoped<IProductService, ProductService>()
            .AddScoped<IProductVariantService, ProductVariantService>()
            .AddScoped<IQRCodeService, QRCodeService>()
            .AddScoped<IProductAttributeService, ProductAttributeService>()
            .AddScoped<IIotWeightService, IotWeightService>()
            .AddScoped<IIotDeviceService, IotDeviceService>()
            .AddScoped<IIotDeviceCommandService, IotDeviceCommandService>()
            .AddScoped<IWarehouseService, WarehouseService>()
            .AddScoped<ILocationService, LocationService>()
            .AddScoped<IInventoryService, InventoryService>()
            .AddScoped<IInventoryTransactionService, InventoryTransactionService>();


        services.AddFluentValidationAutoValidation(options => options.DisableDataAnnotationsValidation = true);
        services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

        return services;
    }
}
