using System;
using System.Data;
using System.Reflection;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.SeedData;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

using Backend.Application.Interfaces;
using Microsoft.AspNetCore.Http;

namespace Backend.Infrastructure.Persistence;

public class BackendContext : DbContext, IApplicationDbContext
{
    private readonly IHttpContextAccessor? _httpContextAccessor;

    public BackendContext(DbContextOptions<BackendContext> options, IHttpContextAccessor? httpContextAccessor = null) : base(options)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public virtual DbSet<Domain.Entities.Action> Actions { get; set; }
    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }
    public virtual DbSet<FileUpload> FileUploads { get; set; }
    public virtual DbSet<FolderUpload> FolderUploads { get; set; }
    public virtual DbSet<Menu> Menus { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<NotificationCategory> NotificationCategories { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<SystemConfig> SystemConfigs { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserDevice> UserDevices { get; set; }
    public virtual DbSet<UserNotification> UserNotifications { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }
    public virtual DbSet<UserSession> UserSessions { get; set; }
    public virtual DbSet<UserStatus> UserStatuses { get; set; }
    public virtual DbSet<UserVerificationToken> UserVerificationTokens { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<NotificationType> NotificationTypes { get; set; }
    public virtual DbSet<DeliveryNote> DeliveryNotes { get; set; }
    public virtual DbSet<FcmNotificationLog> FcmNotificationLogs { get; set; }
    public virtual DbSet<Inventory> Inventories { get; set; }
    public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }
    public virtual DbSet<Location> Locations { get; set; }
    public virtual DbSet<Product> Products { get; set; }
    public virtual DbSet<ProductAttribute> ProductAttributes { get; set; }
    public virtual DbSet<ProductCategory> ProductCategories { get; set; }
    public virtual DbSet<ProductVariant> ProductVariants { get; set; }
    public virtual DbSet<InboundOrder> InboundOrders { get; set; }
    public virtual DbSet<InboundOrderItem> InboundOrderItems { get; set; }
    public virtual DbSet<InboundOrderStatus> InboundOrderStatuses { get; set; }
    public virtual DbSet<OutboundOrder> OutboundOrders { get; set; }
    public virtual DbSet<OutboundOrderItem> OutboundOrderItems { get; set; }
    public virtual DbSet<OutboundOrderStatus> OutboundOrderStatuses { get; set; }
    public virtual DbSet<OutboundOrderItemAllocation> OutboundOrderItemAllocations { get; set; }
    public virtual DbSet<StockAlertConfig> StockAlertConfigs { get; set; }
    public virtual DbSet<StockTake> StockTakes { get; set; }
    public virtual DbSet<StockTakeItem> StockTakeItems { get; set; }
    public virtual DbSet<StockTakeStatus> StockTakeStatuses { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<UnitOfMeasure> UnitOfMeasures { get; set; }
    public virtual DbSet<Warehouse> Warehouses { get; set; }
    public virtual DbSet<Alert> Alerts { get; set; }
    public virtual DbSet<CustomerReturnOrder> CustomerReturnOrders { get; set; }
    public virtual DbSet<CustomerReturnOrderItem> CustomerReturnOrderItems { get; set; }
    public virtual DbSet<CustomerReturnOrderStatus> CustomerReturnOrderStatuses { get; set; }
    public virtual DbSet<CustomerReturnOrderItemAllocation> CustomerReturnOrderItemAllocations { get; set; }
    public virtual DbSet<ReturnToSupplierOrder> ReturnToSupplierOrders { get; set; }
    public virtual DbSet<ReturnToSupplierOrderItem> ReturnToSupplierOrderItems { get; set; }
    public virtual DbSet<ReturnToSupplierOrderStatus> ReturnToSupplierOrderStatuses { get; set; }

    // ── Nhóm A: Đặc thù lúa/gạo ──────────────────────────────────────────────
    public virtual DbSet<Organization> Organizations { get; set; }
    public virtual DbSet<Farmer> Farmers { get; set; }
    public virtual DbSet<RiceVariety> RiceVarieties { get; set; }
    public virtual DbSet<PaddyPurchaseScheduleStatus> PaddyPurchaseScheduleStatuses { get; set; }
    public virtual DbSet<PaddyPurchaseSchedule> PaddyPurchaseSchedules { get; set; }
    public virtual DbSet<PaddyPurchaseReceipt> PaddyPurchaseReceipts { get; set; }
    public virtual DbSet<LotStatus> LotStatuses { get; set; }
    public virtual DbSet<PaddyLot> PaddyLots { get; set; }
    public virtual DbSet<PaddyLotBag> PaddyLotBags { get; set; }
    public virtual DbSet<PaddyLotBagContent> PaddyLotBagContents { get; set; }
    public virtual DbSet<PaddyLotBagMovement> PaddyLotBagMovements { get; set; }
    public virtual DbSet<PaddyLotBagAllocation> PaddyLotBagAllocations { get; set; }
    public virtual DbSet<QualityInspection> QualityInspections { get; set; }
    public virtual DbSet<MillingOrderStatus> MillingOrderStatuses { get; set; }
    public virtual DbSet<MillingYieldConfig> MillingYieldConfigs { get; set; }
    public virtual DbSet<MillingOrder> MillingOrders { get; set; }
    public virtual DbSet<MillingOrderInput> MillingOrderInputs { get; set; }
    public virtual DbSet<MillingOrderOutput> MillingOrderOutputs { get; set; }
    public virtual DbSet<PartyDebt> PartyDebts { get; set; }
    public virtual DbSet<DebtTransaction> DebtTransactions { get; set; }
    public virtual DbSet<StockTransferStatus> StockTransferStatuses { get; set; }
    public virtual DbSet<StockTransfer> StockTransfers { get; set; }
    public virtual DbSet<StockTransferItem> StockTransferItems { get; set; }

    // ── Nhóm B: Chứng từ nguồn & Multi-tenant ─────────────────────────────────
    public virtual DbSet<Customer> Customers { get; set; }
    public virtual DbSet<SalesOrderStatus> SalesOrderStatuses { get; set; }
    public virtual DbSet<SalesOrder> SalesOrders { get; set; }
    public virtual DbSet<SalesOrderItem> SalesOrderItems { get; set; }
    public virtual DbSet<PurchaseOrderStatus> PurchaseOrderStatuses { get; set; }
    public virtual DbSet<PurchaseOrder> PurchaseOrders { get; set; }
    public virtual DbSet<PurchaseOrderItem> PurchaseOrderItems { get; set; }

    // new putaway tables
    public virtual DbSet<PutawayDecision> PutawayDecisions { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        modelBuilder.HasDbFunction(typeof(DateTimeExtensions).GetMethod(nameof(DateTimeExtensions.ToVietnameseDateTime)))
            .HasTranslation(e =>
            {
                return new SqlFunctionExpression(
                    functionName: "DATE_FORMAT",
                    arguments: new[]
                    {
                            e.First(),
                            new SqlFragmentExpression("'%d/%m/%Y %H:%i:%s'")
                    },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, false },
                    type: typeof(string),
                    typeMapping: new StringTypeMapping("nvarchar(max)", DbType.String)
                 );
            });

        modelBuilder.HasDbFunction(typeof(DateTimeExtensions).GetMethod(nameof(DateTimeExtensions.ToVietnameseDateOffset)))
            .HasTranslation(e =>
            {
                return new SqlFunctionExpression(
                    functionName: "DATE_FORMAT",
                    arguments: new[]{
                            e.First(),
                            new SqlFragmentExpression("'%d/%m/%Y'")

                    },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, false },
                    type: typeof(string),
                    typeMapping: new StringTypeMapping("nvarchar(max)", DbType.String)
                );
            });

        modelBuilder.HasDbFunction(typeof(DateTimeExtensions).GetMethod(nameof(DateTimeExtensions.ToVietnameseDateTime)))
            .HasTranslation(e =>
            {
                return new SqlFunctionExpression(
                    functionName: "DATE_FORMAT",
                    arguments: new[]
                    {
                            e.First(),
                            new SqlFragmentExpression("'%d/%m/%Y %H:%i:%s'")
                    },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, false },
                    type: typeof(string),
                    typeMapping: new StringTypeMapping("nvarchar(max)", DbType.String)
                 );
            });

        modelBuilder.HasDbFunction(typeof(DateTimeExtensions).GetMethod(nameof(DateTimeExtensions.ToVietnameseDate)))
            .HasTranslation(e =>
            {
                return new SqlFunctionExpression(
                    functionName: "DATE_FORMAT",
                    arguments: new[]{
                            e.First(),
                            new SqlFragmentExpression("'%d/%m/%Y'")

                    },
                    nullable: true,
                    argumentsPropagateNullability: new[] { true, false },
                    type: typeof(string),
                    typeMapping: new StringTypeMapping("nvarchar(max)", DbType.String)
                );
            });
        modelBuilder.Entity<Domain.Entities.Action>().HasData(ActionSeed.GetActions());
        modelBuilder.Entity<Role>().HasData(RoleSeed.GetRoles());
        modelBuilder.Entity<UserRole>().HasData(UserRoleSeed.GetUserRoles());
        modelBuilder.Entity<UserStatus>().HasData(UserStatusSeed.GetUserStatuses());
        modelBuilder.Entity<User>().HasData(UserSeed.GetUsers());
        modelBuilder.Entity<StockTakeStatus>().HasData(StockTakeStatusSeed.GetStockTakeStatuses());
        // ── Seed data lúa/gạo ────────────────────────────────────────────────
        modelBuilder.Entity<Organization>().HasData(OrganizationSeed.GetOrganizations());
        modelBuilder.Entity<PaddyPurchaseScheduleStatus>().HasData(PaddyPurchaseScheduleStatusSeed.GetStatuses());
        modelBuilder.Entity<LotStatus>().HasData(LotStatusSeed.GetStatuses());
        modelBuilder.Entity<MillingOrderStatus>().HasData(MillingOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<SalesOrderStatus>().HasData(SalesOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<PurchaseOrderStatus>().HasData(PurchaseOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<StockTransferStatus>().HasData(StockTransferStatusSeed.GetStatuses());
        modelBuilder.Entity<OutboundOrderStatus>().HasData(OutboundOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<InboundOrderStatus>().HasData(InboundOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<CustomerReturnOrderStatus>().HasData(CustomerReturnOrderStatusSeed.GetStatuses());
        modelBuilder.Entity<ReturnToSupplierOrderStatus>().HasData(ReturnToSupplierOrderStatusSeed.GetStatuses());
        // ── RC-2 fix: Seed Product/Variant đại diện lúa/gạo/phụ phẩm ────────
        // Thứ tự quan trọng: UoM → Category → Product → Variant (theo FK chain)
        modelBuilder.Entity<UnitOfMeasure>().HasData(UnitOfMeasureSeed.GetUnits());
        modelBuilder.Entity<ProductCategory>().HasData(ProductCategorySeed.GetCategories());
        modelBuilder.Entity<Product>().HasData(ProductSeed.GetProducts());
        modelBuilder.Entity<ProductVariant>().HasData(ProductVariantSeed.GetVariants());
        
        // Seed Notification (done in migration SeedNotificationData.cs)
        
        base.OnModelCreating(modelBuilder);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // Check if we need to bypass occupancy interception (e.g. during raw SQL updates in store-in confirm)
        var httpContext = _httpContextAccessor?.HttpContext;
        var bypass = httpContext?.Items.ContainsKey("BypassLocationOccupancyInterceptor") == true;

        if (!bypass)
        {
            var inventoryEntries = ChangeTracker.Entries<Inventory>()
                .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified || e.State == EntityState.Deleted)
                .ToList();

            foreach (var entry in inventoryEntries)
            {
                decimal oldQty = 0;
                int? oldLocId = null;
                int? oldPvId = null;

                if (entry.State == EntityState.Modified || entry.State == EntityState.Deleted)
                {
                    oldQty = (decimal)entry.OriginalValues[nameof(Inventory.QuantityOnHand)];
                    oldLocId = (int?)entry.OriginalValues[nameof(Inventory.LocationId)];
                    oldPvId = (int?)entry.OriginalValues[nameof(Inventory.ProductVariantId)];
                }

                decimal newQty = 0;
                int? newLocId = null;
                int? newPvId = null;

                if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                {
                    newQty = entry.Entity.QuantityOnHand;
                    newLocId = entry.Entity.LocationId;
                    newPvId = entry.Entity.ProductVariantId;
                }

                if (oldLocId.HasValue && oldLocId == newLocId)
                {
                    var diff = newQty - oldQty;
                    if (diff != 0)
                    {
                        await UpdateLocationOccupancyInternalAsync(oldLocId.Value, diff, newPvId!.Value, cancellationToken);
                    }
                }
                else
                {
                    if (oldLocId.HasValue && oldQty > 0)
                    {
                        await UpdateLocationOccupancyInternalAsync(oldLocId.Value, -oldQty, oldPvId!.Value, cancellationToken);
                    }
                    if (newLocId.HasValue && newQty > 0)
                    {
                        await UpdateLocationOccupancyInternalAsync(newLocId.Value, newQty, newPvId!.Value, cancellationToken);
                    }
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    private async Task UpdateLocationOccupancyInternalAsync(int locationId, decimal delta, int productVariantId, CancellationToken cancellationToken)
    {
        var loc = ChangeTracker.Entries<Location>()
            .FirstOrDefault(e => e.Entity.Id == locationId)?.Entity;

        if (loc == null)
        {
            loc = await Locations.FindAsync(new object[] { locationId }, cancellationToken);
        }

        if (loc != null)
        {
            loc.CurrentOccupancy += delta;
            if (loc.CurrentOccupancy <= 0)
            {
                loc.CurrentOccupancy = 0;
                loc.CurrentProductVariantId = null;
            }
            else
            {
                loc.CurrentProductVariantId = productVariantId;
            }
        }
    }

    public async Task<int> ExecuteSqlRawAsync(string sql, object[] parameters, CancellationToken cancellationToken = default)
    {
        return await Database.ExecuteSqlRawAsync(sql, parameters, cancellationToken);
    }
}
