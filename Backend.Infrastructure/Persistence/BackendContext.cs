using System;
using System.Data;
using System.Reflection;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence.SeedData;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query.SqlExpressions;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.Infrastructure.Persistence;

public class BackendContext : DbContext
{
    public BackendContext(DbContextOptions<BackendContext> options) : base(options)
    {

    }

    public virtual DbSet<Domain.Entities.Action> Actions { get; set; }
    public virtual DbSet<ActivityLog> ActivityLogs { get; set; }
    public virtual DbSet<BlogPostCategory> BlogPostCategories { get; set; }
    public virtual DbSet<BlogPostComment> BlogPostComments { get; set; }
    public virtual DbSet<BlogPostLayout> BlogPostLayouts { get; set; }
    public virtual DbSet<BlogPost> BlogPosts { get; set; }
    public virtual DbSet<BlogPostStatus> BlogPostStatuses { get; set; }
    public virtual DbSet<BlogPostTag> BlogPostTags { get; set; }
    public virtual DbSet<Feedback> Feedbacks { get; set; }
    public virtual DbSet<FileUpload> FileUploads { get; set; }
    public virtual DbSet<FolderUpload> FolderUploads { get; set; }
    public virtual DbSet<Menu> Menus { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<NotificationCategory> NotificationCategories { get; set; }
    public virtual DbSet<Permission> Permissions { get; set; }
    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<SystemConfig> SystemConfigs { get; set; }
    public virtual DbSet<Tag> Tags { get; set; }
    public virtual DbSet<TagType> TagTypes { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<UserDevice> UserDevices { get; set; }
    public virtual DbSet<UserNotification> UserNotifications { get; set; }
    public virtual DbSet<UserRole> UserRoles { get; set; }
    public virtual DbSet<UserSession> UserSessions { get; set; }
    public virtual DbSet<UserStatus> UserStatuses { get; set; }
    public virtual DbSet<UserVerificationToken> UserVerificationTokens { get; set; }
    public virtual DbSet<Province> Provinces { get; set; }
    public virtual DbSet<Ward> Wards { get; set; }
    public virtual DbSet<PaymentTransaction> PaymentTransactions { get; set; }
    public virtual DbSet<PaymentStatus> PaymentStatuses { get; set; }
    public virtual DbSet<PaymentMethod> PaymentMethods { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<NotificationType> NotificationTypes { get; set; }
    public virtual DbSet<DeliveryNote> DeliveryNotes { get; set; }
    public virtual DbSet<FcmNotificationLog> FcmNotificationLogs { get; set; }
    public virtual DbSet<Inventory> Inventories { get; set; }
    public virtual DbSet<InventoryTransaction> InventoryTransactions { get; set; }
    public virtual DbSet<IotDevice> IotDevices { get; set; }
    public virtual DbSet<IotDeviceCommand> IotDeviceCommands { get; set; }
    public virtual DbSet<IotWeightLog> IotWeightLogs { get; set; }
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
    public virtual DbSet<StockAlertConfig> StockAlertConfigs { get; set; }
    public virtual DbSet<StockTake> StockTakes { get; set; }
    public virtual DbSet<StockTakeItem> StockTakeItems { get; set; }
    public virtual DbSet<StockTakeStatus> StockTakeStatuses { get; set; }
    public virtual DbSet<Supplier> Suppliers { get; set; }
    public virtual DbSet<UnitOfMeasure> UnitOfMeasures { get; set; }
    public virtual DbSet<Warehouse> Warehouses { get; set; }

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
        base.OnModelCreating(modelBuilder);
    }
}
