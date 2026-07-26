using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.Putaway;
using Backend.Application.Implements;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Repositories;
using Backend.Share.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Putaway;

public class PutawaySuggestionServiceIntegrationTests
{
    private readonly Mock<ILogger<PutawaySuggestionService>> _loggerMock = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<IScheduledJobService> _scheduledJobServiceMock = new();

    public PutawaySuggestionServiceIntegrationTests()
    {
        // Mock User ID = 1
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, "1") };
        var identity = new ClaimsIdentity(claims, "TestAuth");
        var principal = new ClaimsPrincipal(identity);
        var httpContext = new DefaultHttpContext { User = principal };
        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContext);
    }

    private BackendContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var context = new BackendContext(options);
        return context;
    }

    private async Task SeedDataAsync(BackendContext context)
    {
        // 1. Seed Warehouses
        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH-01", Name = "Kho Thô 1", IsActive = true });

        // 2. Seed Schedule Statuses (Adding required Color property)
        context.PaddyPurchaseScheduleStatuses.AddRange(
            new PaddyPurchaseScheduleStatus { Id = 1, Code = "NEW", Name = "Mới tạo", Color = "#6B7280", CreatedDate = DateTime.UtcNow },
            new PaddyPurchaseScheduleStatus { Id = 4, Code = "WEIGHED", Name = "Đã cân hàng", Color = "#8B5CF6", CreatedDate = DateTime.UtcNow },
            new PaddyPurchaseScheduleStatus { Id = 5, Code = "STOCKED", Name = "Đã nhập kho", Color = "#10B981", CreatedDate = DateTime.UtcNow },
            new PaddyPurchaseScheduleStatus { Id = 6, Code = "CANCELLED", Name = "Hủy", Color = "#EF4444", CreatedDate = DateTime.UtcNow },
            new PaddyPurchaseScheduleStatus { Id = 7, Code = "PARTIALLY_STOCKED", Name = "Nhập một phần", Color = "#06B6D4", CreatedDate = DateTime.UtcNow }
        );

        // 3. Seed Schedule
        context.PaddyPurchaseSchedules.Add(new Backend.Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 12,
            ScheduleCode = "SCH-12",
            StatusId = 4, // WEIGHED
            FarmerId = 1,
            EstimatedQtyKg = 3000,
            CreatedDate = DateTime.UtcNow
        });

        // 4. Seed Receipt
        context.PaddyPurchaseReceipts.Add(new PaddyPurchaseReceipt
        {
            Id = 18,
            ReceiptCode = "PPR-18",
            ScheduleId = 12,
            WarehouseId = 1,
            ActualWeightKg = 3000,
            TotalAmount = 19500000,
            CreatedDate = DateTime.UtcNow
        });

        // 5. Seed LotStatus
        context.LotStatuses.Add(new LotStatus { Id = 1, Code = "IN_STOCK", Name = "Trong kho", Color = "#10B981", IsSellable = true, CreatedDate = DateTime.UtcNow });

        // 6. Seed PaddyLot (Adding required LotType and StatusId)
        context.PaddyLots.Add(new Backend.Domain.Entities.PaddyLot
        {
            Id = 35,
            LotCode = "LOT-35",
            LotType = "PADDY",
            StatusId = 1,
            SourceReceiptId = 18,
            ProductVariantId = 105,
            WarehouseId = 1,
            CostPricePerKg = 6500,
            CreatedDate = DateTime.UtcNow
        });

        // 6. Seed Locations
        context.Locations.Add(new Location
        {
            Id = 4,
            WarehouseId = 1,
            ZoneName = "Khu A",
            MaxCapacity = 50000,
            IsActive = true
        });

        // 7. Seed Inventory (Buffer)
        context.Inventories.Add(new Backend.Domain.Entities.Inventory
        {
            WarehouseId = 1,
            LocationId = null, // Buffer Zone
            ProductVariantId = 105,
            PaddyLotId = 35,
            QuantityOnHand = 3000,
            CostPrice = 6500
        });

        await context.SaveChangesAsync();
    }

    [Fact]
    [Trait("Service", "PutawayIntegration")]
    public async Task ConfirmStoreInAsync_EFCoreInMemory_UpdatesScheduleStatusCorrectly()
    {
        // Arrange
        using var context = CreateInMemoryContext();
        await SeedDataAsync(context);

        var unitOfWorkMock = new Mock<IUnitOfWork>();
        var locationRepo = new LocationRepository(context, unitOfWorkMock.Object);

        var service = new PutawaySuggestionService(
            context,
            locationRepo,
            _httpContextAccessorMock.Object,
            _scheduledJobServiceMock.Object,
            _loggerMock.Object
        );

        // Act - Store-In 1 (Cất trước 1000 kg)
        var request1 = new ConfirmStoreInRequest
        {
            ProductVariantId = 105,
            SelectedLocationId = 4,
            SuggestedLocationId = 4,
            WeightKg = 1000,
            PaddyLotId = 35,
            OverrideReason = ""
        };

        var result1 = await service.ConfirmStoreInAsync("PADDY_PURCHASE", 18, request1, CancellationToken.None);

        // Assert 1
        result1.IsSucceeded.Should().BeTrue(because: result1.Message);

        // Kiểm chứng Schedule chuyển sang PARTIALLY_STOCKED (7)
        var scheduleAfter1 = await context.PaddyPurchaseSchedules.FindAsync(12);
        scheduleAfter1.Should().NotBeNull();
        scheduleAfter1!.StatusId.Should().Be(7); // PARTIALLY_STOCKED

        // Kiểm chứng Tồn kho đệm giảm từ 3000 -> 2000
        var bufferInvAfter1 = await context.Inventories
            .FirstOrDefaultAsync(x => x.WarehouseId == 1 && x.LocationId == null && x.PaddyLotId == 35);
        bufferInvAfter1.Should().NotBeNull();
        bufferInvAfter1!.QuantityOnHand.Should().Be(3000);

        // Kiểm chứng Tồn kho thật tăng lên 1000
        var realInvAfter1 = await context.Inventories
            .FirstOrDefaultAsync(x => x.WarehouseId == 1 && x.LocationId == 4 && x.PaddyLotId == 35);
        realInvAfter1.Should().NotBeNull();
        realInvAfter1!.QuantityOnHand.Should().Be(1000);

        // Act - Store-In 2 (Cất nốt 2000 kg còn lại)
        var request2 = new ConfirmStoreInRequest
        {
            ProductVariantId = 105,
            SelectedLocationId = 4,
            SuggestedLocationId = 4,
            WeightKg = 2000,
            PaddyLotId = 35,
            OverrideReason = ""
        };

        var result2 = await service.ConfirmStoreInAsync("PADDY_PURCHASE", 18, request2, CancellationToken.None);

        // Assert 2
        result2.IsSucceeded.Should().BeTrue();

        // Kiểm chứng Schedule chuyển sang STOCKED (5)
        var scheduleAfter2 = await context.PaddyPurchaseSchedules.FindAsync(12);
        scheduleAfter2.Should().NotBeNull();
        scheduleAfter2!.StatusId.Should().Be(5); // STOCKED

        // Kiểm chứng Tồn kho đệm giảm về 0
        var bufferInvAfter2 = await context.Inventories
            .FirstOrDefaultAsync(x => x.WarehouseId == 1 && x.LocationId == null && x.PaddyLotId == 35);
        bufferInvAfter2.Should().NotBeNull();
        bufferInvAfter2!.QuantityOnHand.Should().Be(3000);

        // Kiểm chứng Tồn kho thật tăng lên 3000
        var realInvAfter2 = await context.Inventories
            .FirstOrDefaultAsync(x => x.WarehouseId == 1 && x.LocationId == 4 && x.PaddyLotId == 35);
        realInvAfter2.Should().NotBeNull();
        realInvAfter2!.QuantityOnHand.Should().Be(3000);
    }
}
