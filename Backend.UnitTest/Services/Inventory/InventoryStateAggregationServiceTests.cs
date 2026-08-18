using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Implements;
using Backend.Application.Constants;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Inventory;

public class InventoryStateAggregationServiceTests
{
    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new BackendContext(options);
    }

    private async Task SeedBaseDataAsync(BackendContext context)
    {
        // Seed Warehouse
        var warehouse = new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH01", Name = "Warehouse 1", IsActive = true, IsDeleted = false };
        context.Warehouses.Add(warehouse);

        // Seed Product & Variant
        var product = new Backend.Domain.Entities.Product { Id = 1, Name = "Product 1", IsActive = true, IsDeleted = false };
        var variant = new ProductVariant
        {
            Id = 1,
            ProductId = 1,
            SKU = "PV01",
            Name = "Variant 1",
            IsActive = true,
            MinStockLevel = 100m,
            IsDeleted = false
        };
        context.Products.Add(product);
        context.ProductVariants.Add(variant);

        // Seed LotStatus
        var sellableStatus = new LotStatus { Id = 1, Name = "Được phép bán", Code = LotStatusCodeConstants.InStock, Color = "#10B981", IsSellable = true };
        var quarantineStatus = new LotStatus { Id = 2, Name = "Cách ly", Code = LotStatusCodeConstants.Quarantine, Color = "#EF4444", IsSellable = false };
        context.LotStatuses.AddRange(sellableStatus, quarantineStatus);

        // Seed Locations
        var normalLocation = new Location { Id = 1, WarehouseId = 1, ZoneName = "Zone A", IsActive = true, IsQuarantine = false, IsDeleted = false, CurrentOccupancy = 0 };
        var quarantineLocation = new Location { Id = 2, WarehouseId = 1, ZoneName = "Zone B", IsActive = true, IsQuarantine = true, IsDeleted = false, CurrentOccupancy = 0 };
        context.Locations.AddRange(normalLocation, quarantineLocation);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task GetAggregatesAsync_LotNormal_LocationQuarantine_ShouldBeQuarantinedOnly()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Lot normal (sellable)
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 1,
            LotCode = "LOT01",
            LotType = "PADDY",
            WarehouseId = 1,
            ProductVariantId = 1,
            StatusId = 1, // sellable
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // Inventory in quarantine location
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 2, // quarantine location
            PaddyLotId = 1,
            QuantityOnHand = 500m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var service = new InventoryStateAggregationService(context, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        // Act
        var result = await service.GetAggregatesAsync(1, 1, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var aggregate = result.First();
        aggregate.QuarantinedKg.Should().Be(500m);
        aggregate.SellableOnHandKg.Should().Be(0m);
        aggregate.AvailableKg.Should().Be(0m);
    }

    [Fact]
    public async Task GetAggregatesAsync_LotQuarantine_LocationNormal_ShouldBeQuarantinedOnly()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Lot quarantine
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 2,
            LotCode = "LOT02",
            LotType = "PADDY",
            WarehouseId = 1,
            ProductVariantId = 1,
            StatusId = 2, // quarantine status
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // Inventory in normal location
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1, // normal location
            PaddyLotId = 2,
            QuantityOnHand = 600m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var service = new InventoryStateAggregationService(context, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        // Act
        var result = await service.GetAggregatesAsync(1, 1, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var aggregate = result.First();
        aggregate.QuarantinedKg.Should().Be(600m);
        aggregate.SellableOnHandKg.Should().Be(0m);
        aggregate.AvailableKg.Should().Be(0m);
    }

    [Fact]
    public async Task GetAggregatesAsync_LotQuarantine_LocationQuarantine_ShouldNotDoubleCount()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Lot quarantine
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 3,
            LotCode = "LOT03",
            LotType = "PADDY",
            WarehouseId = 1,
            ProductVariantId = 1,
            StatusId = 2, // quarantine status
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        // Inventory in quarantine location
        var inventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 2, // quarantine location
            PaddyLotId = 3,
            QuantityOnHand = 700m,
            QuantityReserved = 0m,
            IsDeleted = false
        };
        context.Inventories.Add(inventory);
        await context.SaveChangesAsync();

        var service = new InventoryStateAggregationService(context, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        // Act
        var result = await service.GetAggregatesAsync(1, 1, CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var aggregate = result.First();
        aggregate.TotalOnHandKg.Should().Be(700m);
        aggregate.QuarantinedKg.Should().Be(700m);
        aggregate.SellableOnHandKg.Should().Be(0m);
        aggregate.AvailableKg.Should().Be(0m);
    }

    [Fact]
    public async Task GetAggregatesAsync_AvailableKg_ShouldDeductReservedCorrectlyAndNotCrossDeduct()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Lot normal (sellable)
        var lotNormal = new Backend.Domain.Entities.PaddyLot
        {
            Id = 10,
            LotCode = "LOT10",
            LotType = "PADDY",
            WarehouseId = 1,
            ProductVariantId = 1,
            StatusId = 1, // sellable
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        // Lot quarantined
        var lotQuarantine = new Backend.Domain.Entities.PaddyLot
        {
            Id = 11,
            LotCode = "LOT11",
            LotType = "PADDY",
            WarehouseId = 1,
            ProductVariantId = 1,
            StatusId = 2, // quarantine
            RemainingWeightKg = 1000m,
            IsDeleted = false
        };
        context.PaddyLots.AddRange(lotNormal, lotQuarantine);

        // Normal row with reserved (SellableOnHand = 500, Reserved = 100)
        var normalInventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1, // normal
            PaddyLotId = 10,
            QuantityOnHand = 500m,
            QuantityReserved = 100m,
            IsDeleted = false
        };

        // Quarantine row with reserved (Quarantined = 400, Reserved = 150) -> Anomaly!
        var quarantineInventory = new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = 1, // normal location but lot is quarantine status
            PaddyLotId = 11,
            QuantityOnHand = 400m,
            QuantityReserved = 150m,
            IsDeleted = false
        };

        context.Inventories.AddRange(normalInventory, quarantineInventory);
        await context.SaveChangesAsync();

        var service = new InventoryStateAggregationService(context, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);

        // Act
        var result = await service.GetAggregatesAsync(1, 1, CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);

        var normalAgg = result.First(x => x.PaddyLotId == 10);
        normalAgg.SellableOnHandKg.Should().Be(500m);
        normalAgg.ReservedSellableKg.Should().Be(100m);
        normalAgg.AvailableKg.Should().Be(400m); // 500 - 100

        var quarantineAgg = result.First(x => x.PaddyLotId == 11);
        quarantineAgg.QuarantinedKg.Should().Be(400m);
        quarantineAgg.ReservedSellableKg.Should().Be(0m); // quarantined row shouldn't have reserved sellable
        quarantineAgg.AvailableKg.Should().Be(0m); // quarantined is not sellable
    }
}
