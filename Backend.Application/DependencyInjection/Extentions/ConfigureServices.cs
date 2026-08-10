using System;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Application.Validators.Auths;
using Backend.Application.BackgroundJobs.IntakeBottleneck;
using Backend.Application.BackgroundJobs.LotQualityRecheck;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
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
            .AddScoped<INotificationDispatcher, NotificationDispatcher>()
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
            .AddScoped<IInboundOrderStatusService, InboundOrderStatusService>()
            .AddScoped<IOutboundOrderStatusService, OutboundOrderStatusService>()
            .AddScoped<IStockTakeStatusService, StockTakeStatusService>()
            .AddScoped<ICustomerReturnOrderStatusService, CustomerReturnOrderStatusService>()
            .AddScoped<IReturnToSupplierOrderStatusService, ReturnToSupplierOrderStatusService>()
            .AddScoped<IPaddyPurchaseScheduleStatusService, PaddyPurchaseScheduleStatusService>()
            .AddScoped<ILotStatusService, LotStatusService>()
            .AddScoped<IMillingOrderStatusService, MillingOrderStatusService>()
            .AddScoped<IStockTransferStatusService, StockTransferStatusService>()
            .AddScoped<ISalesOrderStatusService, SalesOrderStatusService>()
            .AddScoped<IPurchaseOrderStatusService, PurchaseOrderStatusService>()
            .AddScoped<IUserVerificationTokenService, UserVerificationTokenService>()
            .AddScoped<IAuthService, AuthService>()
            .AddScoped<IEmailTemplateService, EmailTemplateService>()
            .AddScoped<IAuditLogService, AuditLogService>()
            .AddScoped<INotificationTypeService, NotificationTypeService>()
            .AddScoped<IDashboardService, DashboardService>()
            .AddScoped<ISearchService, SearchService>()
            .AddScoped<IProductCategoryService, ProductCategoryService>()
            .AddScoped<IProductService, ProductService>()
            .AddScoped<IProductVariantService, ProductVariantService>()
            .AddScoped<IQRCodeService, QRCodeService>()
            .AddScoped<IQrIdentifierService, QrIdentifierService>()
            .AddScoped<IProductAttributeService, ProductAttributeService>()
            .AddScoped<IWarehouseService, WarehouseService>()
            .AddScoped<ILocationService, LocationService>()
            .AddScoped<IInventoryService, InventoryService>()
            .AddScoped<IInventoryTransactionService, InventoryTransactionService>()
            .AddScoped<IInboundOrderService, InboundOrderService>()
            .AddScoped<ISupplierService, SupplierService>()
            .AddScoped<IRiceVarietyService, RiceVarietyService>()
            .AddScoped<IFarmerService, FarmerService>()
            .AddScoped<ICustomerService, CustomerService>()
            .AddScoped<IOrganizationService, OrganizationService>()
            .AddScoped<IStockTakeService, StockTakeService>()
            .AddScoped<IUnitOfMeasureService, UnitOfMeasureService>()
            // ── Rice supply chain services ────────────────────────────────────────────
            .AddScoped<IPaddyLotService, PaddyLotService>()
            .AddScoped<IPaddyLotTraceabilityService, PaddyLotTraceabilityService>()
            .AddScoped<IPaddyLotBagInvariantService, PaddyLotBagInvariantService>()
            .AddScoped<IPaddyPurchaseScheduleService, PaddyPurchaseScheduleService>()
            .AddScoped<IPaddyPurchaseReceiptService, PaddyPurchaseReceiptService>()
            .AddScoped<IMillingOrderService, MillingOrderService>()
            .AddScoped<IPartyDebtService, PartyDebtService>()
            .AddScoped<IQualityInspectionService, QualityInspectionService>()
            .AddScoped<IStockTransferService, StockTransferService>()
            // ── Sales & Outbound flow ─────────────────────────────────────────────────
            .AddScoped<ISalesOrderService, SalesOrderService>()
            .AddScoped<IOutboundOrderService, OutboundOrderService>()
            // ── Purchase & Non-Paddy Inbound flow ─────────────────────────────────────
            .AddScoped<IPurchaseOrderService, PurchaseOrderService>()
            // ── Putaway Suggestion engine ─────────────────────────────────────────────
            .AddScoped<IPutawaySuggestionService, PutawaySuggestionService>()
            // ── Cấu hình rule & cảnh báo (SCR-20/21) ─────────────────────────────────
            .AddScoped<IMillingYieldConfigService, MillingYieldConfigService>()
            .AddScoped<IStockAlertConfigService, StockAlertConfigService>()
            .AddScoped<IInventoryStateAggregationService, InventoryStateAggregationService>()
            .AddScoped<IAlertService, AlertService>()
            .AddScoped<ICustomerReturnOrderService, CustomerReturnOrderService>()
            // ── Trả hàng nhà cung cấp (FE-16) ─────────────────────────────────────────
            .AddScoped<IReturnToSupplierOrderService, ReturnToSupplierOrderService>()
            // ── Intake Bottleneck evaluation (JOB-02) ─────────────────────────────────
            .AddScoped<IIntakeBottleneckCalculator, IntakeBottleneckCalculator>()
            .AddScoped<IIntakeBottleneckQueryService, IntakeBottleneckQueryService>()
            .AddScoped<IIntakeBottleneckEvaluationService, IntakeBottleneckEvaluationService>()
            // ── Lot Quality Recheck & Long Stored evaluation (JOB-03) ─────────────────
            .AddScoped<ILotQualityRecheckRulesEngine, LotQualityRecheckRulesEngine>()
            .AddScoped<ILotQualityRecheckService, LotQualityRecheckService>()
            // ── Debt Due & Overdue Reminder (JOB-04) ──────────────────────────
            .AddScoped<IDebtTransactionEffectResolver, DebtTransactionEffectResolver>()
            .AddScoped<IDebtAgingCalculationService, DebtAgingCalculationService>()
            .AddScoped<IDebtDueOverdueRulesEngine, DebtDueOverdueRulesEngine>()
            .AddScoped<IDebtDueAndOverdueReminderService, DebtDueAndOverdueReminderService>();


        services.AddFluentValidationAutoValidation(options => options.DisableDataAnnotationsValidation = true);
        services.AddValidatorsFromAssemblyContaining<LoginRequestDtoValidator>();

        return services;
    }
}
