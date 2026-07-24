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

namespace Backend.UnitTest.Services.StockTransfers;

[Trait("Service", "StockTransfer")]
public class StockTransferServiceTests
{
    private readonly Mock<IStockTransferRepository> _transferRepo = new();
    private readonly Mock<IRepositoryBase<StockTransferItem, int>> _itemRepo = new();
    private readonly Mock<IRepositoryBase<StockTransferStatus, int>> _statusRepo = new();
    private readonly Mock<IRepositoryBase<LotStatus, int>> _lotStatusRepo = new();
    private readonly Mock<IPaddyLotRepository> _paddyLotRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private StockTransferService Sut() => new(
        _transferRepo.Object,
        _itemRepo.Object,
        _statusRepo.Object,
        _lotStatusRepo.Object,
        _paddyLotRepo.Object,
        _invRepo.Object,
        _invTxRepo.Object,
        _dispatcher.Object);

    [Fact]
    public async Task CreateAsync_SameFromAndToWarehouse_ReturnsBadRequest()
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
    public async Task ConfirmTransferAsync_QuarantinedLot_ReturnsUnprocessable()
    {
        var item = new StockTransferItem { Id = 1, PaddyLotId = 500, WeightKg = 10m, ProductVariantId = 5, FromLocationId = 10, ToLocationId = 20 };
        var transfer = new global::Backend.Domain.Entities.StockTransfer
        {
            Id = 1,
            FromWarehouseId = 1,
            ToWarehouseId = 2,
            StatusId = 1,
            TransferCode = "ST-1",
            StockTransferItems = new List<StockTransferItem> { item }
        };

        _transferRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, object>>[]>()))
            .Returns(new List<global::Backend.Domain.Entities.StockTransfer> { transfer }.AsQueryable().BuildMock());

        _statusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<StockTransferStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<StockTransferStatus, object>>[]>()))
            .ReturnsAsync(new StockTransferStatus { Id = 9, Name = "Completed" });

        _transferRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);

        // Lô thuộc đúng kho nguồn, đủ khối lượng — để chạm tới bước kiểm tra cách ly
        _paddyLotRepo.Setup(r => r.GetByIdAsync(500))
            .ReturnsAsync(new global::Backend.Domain.Entities.PaddyLot
            { Id = 500, LotCode = "LOT-500", WarehouseId = 1, RemainingWeightKg = 100m, StatusId = 3, CostPricePerKg = 10m });

        // Trạng thái lô = QUARANTINE → bị chặn điều chuyển (#2/#11)
        _lotStatusRepo.Setup(r => r.GetByIdAsync(3))
            .ReturnsAsync(new LotStatus { Id = 3, Code = LotStatusCodeConstants.Quarantine, Name = "Cách ly", IsSellable = false });

        var result = await Sut().ConfirmTransferAsync(1, confirmedById: 1);

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmTransferAsync_LotNotInSourceWarehouse_ReturnsUnprocessable()
    {
        var item = new StockTransferItem { Id = 1, PaddyLotId = 500, WeightKg = 10m, ProductVariantId = 5 };
        var transfer = new global::Backend.Domain.Entities.StockTransfer
        {
            Id = 1, FromWarehouseId = 1, ToWarehouseId = 2, StatusId = 1, TransferCode = "ST-1",
            StockTransferItems = new List<StockTransferItem> { item }
        };

        _transferRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.StockTransfer, object>>[]>()))
            .Returns(new List<global::Backend.Domain.Entities.StockTransfer> { transfer }.AsQueryable().BuildMock());

        _statusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<StockTransferStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<StockTransferStatus, object>>[]>()))
            .ReturnsAsync(new StockTransferStatus { Id = 9, Name = "Completed" });

        _transferRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);

        // Lô ở kho khác (2) không phải kho nguồn (1) → bị chặn (#4)
        _paddyLotRepo.Setup(r => r.GetByIdAsync(500))
            .ReturnsAsync(new global::Backend.Domain.Entities.PaddyLot
            { Id = 500, LotCode = "LOT-500", WarehouseId = 2, RemainingWeightKg = 100m, StatusId = 2 });

        var result = await Sut().ConfirmTransferAsync(1, confirmedById: 1);

        result.Status.Should().Be(422);
    }
}
