using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.LowStock;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.LowStock;

public class LowStockDetectionServiceIntegrationTests
{
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<LowStockDetectionService>> _loggerMock = new();
    private readonly Mock<ILogger<LowStockQueryService>> _queryLoggerMock = new();

    public LowStockDetectionServiceIntegrationTests()
    {
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("LowStockDetectionService"))))
            .Returns(_loggerMock.Object);
        _loggerFactoryMock.Setup(x => x.CreateLogger(It.Is<string>(s => s.Contains("LowStockQueryService"))))
            .Returns(_queryLoggerMock.Object);
    }

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private async Task SeedBaseDataAsync(BackendContext context)
    {
        // Warehouse
        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH-01", Name = "Kho 1", IsActive = true, IsDeleted = false });
        
        // Product & Variant
        context.Products.Add(new Backend.Domain.Entities.Product { Id = 1, Name = "Sản phẩm A", IsDeleted = false });
        context.ProductVariants.Add(new ProductVariant
        {
            Id = 1,
            ProductId = 1,
            UnitOfMeasureId = 1,
            SKU = "PV-A",
            Name = "PV-A",
            CostPrice = 10000m,
            SalePrice = 12000m,
            Weight = 1m,
            IsActive = true,
            MinStockLevel = 1000m, // Ngưỡng: 1000 kg
            IsDeleted = false
        });

        // Lot Statuses
        context.LotStatuses.AddRange(
            new LotStatus { Id = 1, Name = "Được bán", Color = "#10B981", IsSellable = true },
            new LotStatus { Id = 2, Name = "Cách ly", Color = "#EF4444", IsSellable = false }
        );

        // Location normal
        context.Locations.Add(new Location { Id = 1, WarehouseId = 1, ZoneName = "Zone A", IsActive = true, IsQuarantine = false, IsDeleted = false });
        
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DetectLowStockAsync_ShouldAggregateStockAndResolveThresholdsRealistically()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Seed 10,000 inventory records to verify performance as well
        var inventories = new List<Backend.Domain.Entities.Inventory>();
        for (int i = 1; i <= 10000; i++)
        {
            inventories.Add(new Backend.Domain.Entities.Inventory
            {
                WarehouseId = 1,
                ProductVariantId = 1,
                LocationId = 1,
                QuantityOnHand = 0.09m, // 10,000 * 0.09 = 900 kg (<= Ngưỡng 1000 kg -> low stock)
                QuantityReserved = 0m,
                IsDeleted = false
            });
        }
        await context.Inventories.AddRangeAsync(inventories);
        await context.SaveChangesAsync();

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Processed.Should().Be(1); // 1 ProductVariant-Warehouse combination aggregated
        result.Created.Should().Be(1); // 1 Alert created

        var alert = await context.Alerts.FirstOrDefaultAsync();
        alert.Should().NotBeNull();
        alert!.Message.Should().Contain("chỉ còn 900 kg khả dụng");
    }

    [Fact]
    public async Task DetectLowStockAsync_WhenNotificationFails_ShouldStillSaveAlertToDatabase()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Stock = 500 <= Threshold = 1000
        context.Inventories.Add(new Backend.Domain.Entities.Inventory { WarehouseId = 1, ProductVariantId = 1, LocationId = 1, QuantityOnHand = 500m, QuantityReserved = 0m, IsDeleted = false });
        await context.SaveChangesAsync();

        // Mock dispatcher to THROW exception when dispatching notification
        _dispatcherMock.Setup(d => d.DispatchAsync(
            It.IsAny<string>(),
            It.IsAny<NotificationTarget>(),
            It.IsAny<object[]>(),
            It.IsAny<string>(),
            It.IsAny<int?>()
        )).ThrowsAsync(new Exception("Firebase connection timeout"));

        var queryService = new LowStockQueryService(context, _loggerFactoryMock.Object);
        var service = new LowStockDetectionService(context, queryService, _dispatcherMock.Object, _loggerFactoryMock.Object);

        // Act
        var result = await service.DetectLowStockAsync(CancellationToken.None);

        // Assert
        result.Created.Should().Be(1);
        
        // Alert should be saved in DB successfully despite the notification failure
        var alert = await context.Alerts.FirstOrDefaultAsync(a => a.WarehouseId == 1 && a.ProductVariantId == 1);
        alert.Should().NotBeNull();
        alert!.Status.Should().Be(AlertConstants.Status.Open);
    }
}
