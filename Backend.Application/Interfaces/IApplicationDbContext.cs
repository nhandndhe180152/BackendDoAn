using System.Threading;
using System.Threading.Tasks;
using Backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Backend.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<Domain.Entities.Action> Actions { get; }
    DbSet<ActivityLog> ActivityLogs { get; }
    DbSet<FileUpload> FileUploads { get; }
    DbSet<FolderUpload> FolderUploads { get; }
    DbSet<Menu> Menus { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationCategory> NotificationCategories { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<Role> Roles { get; }
    DbSet<SystemConfig> SystemConfigs { get; }
    DbSet<User> Users { get; }
    DbSet<UserDevice> UserDevices { get; }
    DbSet<UserNotification> UserNotifications { get; }
    DbSet<UserRole> UserRoles { get; }
    DbSet<UserSession> UserSessions { get; }
    DbSet<UserStatus> UserStatuses { get; }
    DbSet<UserVerificationToken> UserVerificationTokens { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<NotificationType> NotificationTypes { get; }
    DbSet<DeliveryNote> DeliveryNotes { get; }
    DbSet<FcmNotificationLog> FcmNotificationLogs { get; }
    DbSet<Inventory> Inventories { get; }
    DbSet<InventoryTransaction> InventoryTransactions { get; }
    DbSet<Location> Locations { get; }
    DbSet<Product> Products { get; }
    DbSet<ProductAttribute> ProductAttributes { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<ProductVariant> ProductVariants { get; }
    DbSet<InboundOrder> InboundOrders { get; }
    DbSet<InboundOrderItem> InboundOrderItems { get; }
    DbSet<InboundOrderStatus> InboundOrderStatuses { get; }
    DbSet<OutboundOrder> OutboundOrders { get; }
    DbSet<OutboundOrderItem> OutboundOrderItems { get; }
    DbSet<OutboundOrderStatus> OutboundOrderStatuses { get; }
    DbSet<OutboundOrderItemAllocation> OutboundOrderItemAllocations { get; }
    DbSet<StockAlertConfig> StockAlertConfigs { get; }
    DbSet<StockTake> StockTakes { get; }
    DbSet<StockTakeItem> StockTakeItems { get; }
    DbSet<StockTakeStatus> StockTakeStatuses { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<UnitOfMeasure> UnitOfMeasures { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<Alert> Alerts { get; }
    DbSet<CustomerReturnOrder> CustomerReturnOrders { get; }
    DbSet<CustomerReturnOrderItem> CustomerReturnOrderItems { get; }
    DbSet<CustomerReturnOrderStatus> CustomerReturnOrderStatuses { get; }
    DbSet<CustomerReturnOrderItemAllocation> CustomerReturnOrderItemAllocations { get; }
    DbSet<CustomerFeedback> CustomerFeedbacks { get; }
    DbSet<ReturnToSupplierOrder> ReturnToSupplierOrders { get; }
    DbSet<ReturnToSupplierOrderItem> ReturnToSupplierOrderItems { get; }
    DbSet<ReturnToSupplierOrderStatus> ReturnToSupplierOrderStatuses { get; }

    // paddy / lúa
    DbSet<Organization> Organizations { get; }
    DbSet<Farmer> Farmers { get; }
    DbSet<RiceVariety> RiceVarieties { get; }
    DbSet<PaddyPurchaseScheduleStatus> PaddyPurchaseScheduleStatuses { get; }
    DbSet<PaddyPurchaseSchedule> PaddyPurchaseSchedules { get; }
    DbSet<PaddyPurchaseReceipt> PaddyPurchaseReceipts { get; }
    DbSet<LotStatus> LotStatuses { get; }
    DbSet<PaddyLot> PaddyLots { get; }
    DbSet<PaddyLotBag> PaddyLotBags { get; }
    DbSet<PaddyLotBagContent> PaddyLotBagContents { get; }
    DbSet<PaddyLotBagMovement> PaddyLotBagMovements { get; }
    DbSet<PaddyLotBagAllocation> PaddyLotBagAllocations { get; }
    DbSet<QualityInspection> QualityInspections { get; }
    DbSet<QualityInspectionBagResult> QualityInspectionBagResults { get; }
    DbSet<MillingOrderStatus> MillingOrderStatuses { get; }
    DbSet<MillingYieldConfig> MillingYieldConfigs { get; }
    DbSet<MillingOrder> MillingOrders { get; }
    DbSet<MillingOrderInput> MillingOrderInputs { get; }
    DbSet<MillingOrderOutput> MillingOrderOutputs { get; }
    DbSet<PartyDebt> PartyDebts { get; }
    DbSet<DebtTransaction> DebtTransactions { get; }
    DbSet<StockTransferStatus> StockTransferStatuses { get; }
    DbSet<StockTransfer> StockTransfers { get; }
    DbSet<StockTransferItem> StockTransferItems { get; }

    // non-paddy/PO
    DbSet<Customer> Customers { get; }
    DbSet<SalesOrderStatus> SalesOrderStatuses { get; }
    DbSet<SalesOrder> SalesOrders { get; }
    DbSet<SalesOrderItem> SalesOrderItems { get; }
    DbSet<PurchaseOrderStatus> PurchaseOrderStatuses { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }

    // new putaway tables
    DbSet<PutawayDecision> PutawayDecisions { get; }

    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<int> ExecuteSqlRawAsync(string sql, object[] parameters, CancellationToken cancellationToken = default);
}
