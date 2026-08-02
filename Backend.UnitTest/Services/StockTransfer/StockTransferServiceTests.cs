using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTransfers;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;
using WarehouseEntity = Backend.Domain.Entities.Warehouse;

namespace Backend.UnitTest.Services.StockTransfers;

[Trait("Service", "StockTransfer")]
public class StockTransferServiceTests
{
    private readonly Mock<IStockTransferRepository> _transferRepo = new();
    private readonly Mock<IRepositoryBase<StockTransferItem, int>> _itemRepo = new();
    private readonly Mock<IRepositoryBase<StockTransferStatus, int>> _statusRepo = new();
    private readonly Mock<IRepositoryBase<LotStatus, int>> _lotStatusRepo = new();
    private readonly Mock<IRepositoryBase<WarehouseEntity, int>> _warehouseRepo = new();
    private readonly Mock<IRepositoryBase<ProductVariant, int>> _productVariantRepo = new();
    private readonly Mock<IPaddyLotRepository> _paddyLotRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<ILocationRepository> _locationRepo = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private StockTransferService Sut() => new(
        _transferRepo.Object,
        _itemRepo.Object,
        _statusRepo.Object,
        _lotStatusRepo.Object,
        _warehouseRepo.Object,
        _productVariantRepo.Object,
        _paddyLotRepo.Object,
        _invRepo.Object,
        _invTxRepo.Object,
        _locationRepo.Object,
        _dispatcher.Object);

    public StockTransferServiceTests()
    {
        _locationRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Location, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync((Expression<Func<Location, bool>> expr, bool track, Expression<Func<Location, object>>[]? includes) =>
            {
                return new Location { Id = 10, WarehouseId = 1, IsActive = true, MaxCapacity = 10000, SlotCode = "LOC-MOCK" };
            });
    }

    [Fact]
    public async Task CreateAsync_SameFromAndTo_ReturnsBadRequest()
    {
        var dto = new CreateStockTransferDto
        {
            FromWarehouseId = 1,
            ToWarehouseId = 1,
            Items = new List<StockTransferItemDto>()
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateListAsync_NotSupported_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreateStockTransferDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task DispatchAsync_QuarantinedLot_ReturnsUnprocessable()
    {
        var item = new StockTransferItem { Id = 1, PaddyLotId = 500, WeightKg = 10m, ProductVariantId = 5, FromLocationId = 10, ToLocationId = 20 };
        var transfer = new global::Backend.Domain.Entities.StockTransfer
        {
            Id = 1,
            FromWarehouseId = 1,
            ToWarehouseId = 2,
            StatusId = 1,
            Status = new StockTransferStatus { Id = 1, Name = StockTransferStatusNames.Draft },
            TransferCode = "ST-1",
            StockTransferItems = new List<StockTransferItem> { item }
        };

        _transferRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<global::Backend.Domain.Entities.StockTransfer> { transfer }.AsQueryable().BuildMock());

        _warehouseRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Backend.Domain.Entities.Warehouse { Id = 1, IsActive = true, Name = "Kho nguồn", Code = "K1" });
        _warehouseRepo.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Backend.Domain.Entities.Warehouse { Id = 2, IsActive = true, Name = "Kho đích", Code = "K2" });
        _productVariantRepo.Setup(r => r.GetByIdAsync(5))
            .ReturnsAsync(new ProductVariant { Id = 5, IsActive = true, Name = "Gạo", SKU = "GAO" });
        _locationRepo.Setup(r => r.GetByIdAsync(10))
            .ReturnsAsync(new Location { Id = 10, WarehouseId = 1, IsActive = true, ZoneName = "A" });
        _locationRepo.Setup(r => r.GetByIdAsync(20))
            .ReturnsAsync(new Location { Id = 20, WarehouseId = 2, IsActive = true, ZoneName = "B" });

        // Lô thuộc đúng kho nguồn, đủ khối lượng — để chạm tới bước kiểm tra cách ly
        _paddyLotRepo.Setup(r => r.GetByIdAsync(500))
            .ReturnsAsync(new global::Backend.Domain.Entities.PaddyLot
            {
                Id = 500,
                LotCode = "LOT-500",
                WarehouseId = 1,
                LocationId = 10,
                ProductVariantId = 5,
                RemainingWeightKg = 100m,
                StatusId = 3,
                CostPricePerKg = 10m
            });

        // Trạng thái lô = QUARANTINE → bị chặn điều chuyển (#2/#11)
        _lotStatusRepo.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new LotStatus { Id = 3, Code = LotStatusCodeConstants.Quarantine, Name = "Cách ly", IsSellable = false });

        var result = await Sut().DispatchAsync(1, dispatchedById: 1);

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task DispatchAsync_LotNotInSourceWarehouse_ReturnsUnprocessable()
    {
        var item = new StockTransferItem { Id = 1, PaddyLotId = 500, WeightKg = 10m, ProductVariantId = 5 };
        var transfer = new global::Backend.Domain.Entities.StockTransfer
        {
            Id = 1, FromWarehouseId = 1, ToWarehouseId = 2, StatusId = 1,
            Status = new StockTransferStatus { Id = 1, Name = StockTransferStatusNames.Draft },
            TransferCode = "ST-1",
            StockTransferItems = new List<StockTransferItem> { item }
        };

        _transferRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<global::Backend.Domain.Entities.StockTransfer> { transfer }.AsQueryable().BuildMock());

        _warehouseRepo.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(new Backend.Domain.Entities.Warehouse { Id = 1, IsActive = true, Name = "Kho nguồn", Code = "K1" });
        _warehouseRepo.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new Backend.Domain.Entities.Warehouse { Id = 2, IsActive = true, Name = "Kho đích", Code = "K2" });
        _productVariantRepo.Setup(r => r.GetByIdAsync(5))
            .ReturnsAsync(new ProductVariant { Id = 5, IsActive = true, Name = "Gạo", SKU = "GAO" });

        // Lô ở kho khác (2) không phải kho nguồn (1) → bị chặn (#4)
        _paddyLotRepo.Setup(r => r.GetByIdAsync(500))
            .ReturnsAsync(new global::Backend.Domain.Entities.PaddyLot
            { Id = 500, LotCode = "LOT-500", WarehouseId = 2, RemainingWeightKg = 100m, StatusId = 2 });

        var result = await Sut().DispatchAsync(1, dispatchedById: 1);

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmTransferAsync_TargetLocationOverCapacity_ReturnsUnprocessable()
    {
        var item = new StockTransferItem { Id = 1, PaddyLotId = 500, WeightKg = 15m, ProductVariantId = 5, FromLocationId = 10, ToLocationId = 20 };
        var transfer = new global::Backend.Domain.Entities.StockTransfer
        {
            Id = 1, FromWarehouseId = 1, ToWarehouseId = 2, StatusId = 1, TransferCode = "ST-1",
            StockTransferItems = new List<StockTransferItem> { item }
        };

        _transferRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<global::Backend.Domain.Entities.StockTransfer> { transfer }.AsQueryable().BuildMock());

        _statusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<StockTransferStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<StockTransferStatus, object>>[]>()))
            .ReturnsAsync(new StockTransferStatus { Id = 9, Name = "Completed" });

        _transferRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);

        _paddyLotRepo.Setup(r => r.GetByIdAsync(500))
            .ReturnsAsync(new global::Backend.Domain.Entities.PaddyLot
            { Id = 500, LotCode = "LOT-500", WarehouseId = 1, RemainingWeightKg = 100m, StatusId = 2, CostPricePerKg = 10m });

        _lotStatusRepo.Setup(r => r.GetByIdAsync(2))
            .ReturnsAsync(new LotStatus { Id = 2, Code = LotStatusCodeConstants.InStock, Name = "Trong kho", IsSellable = true });

        // Mock target location (ToLocationId = 20) to be near capacity
        // MaxCapacity = 100, CurrentOccupancy = 95. Transfer weight = 15 -> Exceeds capacity!
        _locationRepo.Setup(r => r.FirstOrDefaultAsync(
                It.Is<Expression<Func<Location, bool>>>(expr => expr.Compile().Invoke(new Location { Id = 20, WarehouseId = 2, IsActive = true })),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync(new Location { Id = 20, WarehouseId = 2, IsActive = true, MaxCapacity = 100, CurrentOccupancy = 95, SlotCode = "LOC-20" });

        // Mock source location (FromLocationId = 10) to have enough occupancy to adjust out
        _locationRepo.Setup(r => r.FirstOrDefaultAsync(
                It.Is<Expression<Func<Location, bool>>>(expr => expr.Compile().Invoke(new Location { Id = 10, WarehouseId = 1, IsActive = true })),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync(new Location { Id = 10, WarehouseId = 1, IsActive = true, MaxCapacity = 1000, CurrentOccupancy = 50, SlotCode = "LOC-10" });

        _invRepo.Setup(r => r.GetByVariantWarehouseLocationAsync(5, 1, 10, 500))
            .ReturnsAsync(new Backend.Domain.Entities.Inventory { Id = 1, ProductVariantId = 5, WarehouseId = 1, LocationId = 10, PaddyLotId = 500, QuantityOnHand = 50 });

        var result = await Sut().ReceiveAsync(1, receivedById: 1);

        result.Status.Should().Be(422);
    }
}
