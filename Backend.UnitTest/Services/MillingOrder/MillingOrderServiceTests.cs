using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.MillingOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.MillingOrder;

[Trait("Service", "MillingOrder")]
public class MillingOrderServiceTests
{
    private readonly Mock<IMillingOrderRepository>              _orderRepo    = new();
    private readonly Mock<IPaddyLotRepository>                  _paddyLotRepo = new();
    private readonly Mock<IRepositoryBase<MillingOrderInput,  int>> _inputRepo  = new();
    private readonly Mock<IRepositoryBase<MillingOrderOutput, int>> _outputRepo = new();
    private readonly Mock<IRepositoryBase<MillingOrderStatus, int>> _statusRepo = new();
    private readonly Mock<IRepositoryBase<LotStatus,          int>> _lotStatusRepo = new();
    private readonly Mock<IInventoryRepository>                 _invRepo      = new();
    private readonly Mock<IInventoryTransactionRepository>      _invTxRepo    = new();
    private readonly Mock<ILocationRepository>                  _locationRepo = new();
    private readonly Mock<IMillingYieldConfigRepository>        _yieldRepo    = new();
    private readonly Mock<IRepositoryBase<Alert, int>>           _alertRepo    = new();
    private readonly Mock<INotificationDispatcher>              _dispatcher   = new();

    private MillingOrderService Sut() => new(
        _orderRepo.Object,
        _paddyLotRepo.Object,
        _inputRepo.Object,
        _outputRepo.Object,
        _statusRepo.Object,
        _lotStatusRepo.Object,
        _invRepo.Object,
        _invTxRepo.Object,
        _locationRepo.Object,
        _yieldRepo.Object,
        _alertRepo.Object,
        _dispatcher.Object);

    [Fact]
    public async Task CompleteMillingOrderAsync_OutputPlusLoss_ExceedsInput_Plus2Percent_Returns400()
    {
        // Arrange: input 100 kg. Output 70 + loss 15 + byproducts 20 = 105 > 100 * 1.02 = 102
        var order = new Backend.Domain.Entities.MillingOrder
        {
            Id = 1,
            Status = new MillingOrderStatus { Code = LookupCodes.MillingOrderStatus.InProgress },
            YieldRateUsed = 0.70m,
            TotalRiceOutputKg = 70m,
            MillingOrderInputs = new List<MillingOrderInput>
            {
                new() { PaddyLotId = 1, LocationId = 1, ReservedWeightKg = 100m }
            }
        };

        _orderRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, object>>[]>()))
             .Returns(new List<Backend.Domain.Entities.MillingOrder> { order }.AsQueryable().BuildMock());

        var dto = new CompleteMillingOrderDto
        {
            ActualYieldRate = 70m,
            LossKg = 15m,
            Outputs = new List<MillingOrderOutputItemDto>
            {
                new() { OutputWeightKg = 70m, OutputType = "RICE", IsByproduct = false, ProductVariantId = 1 },
                new() { OutputWeightKg = 20m, OutputType = "BYPRODUCT", IsByproduct = true, ProductVariantId = 2 }
            }
        };

        // Act
        var result = await Sut().CompleteMillingOrderAsync(1, dto, 1);

        // Assert
        result.Status.Should().Be(422);
        result.Message.Should().Contain("Mass balance không hợp lệ");
    }

    [Fact]
    public async Task CompleteMillingOrderAsync_OutputPlusLoss_WithinTolerance_PassesMassBalance()
    {
        // Arrange: input 100 kg. Output 70 + loss 4 + byproducts 25 = 99 kg (within 2% tolerance of 100kg)
        var order = new Backend.Domain.Entities.MillingOrder
        {
            Id = 1,
            Status = new MillingOrderStatus { Code = LookupCodes.MillingOrderStatus.InProgress },
            YieldRateUsed = 0.70m,
            TotalRiceOutputKg = 70m,
            MillingOrderInputs = new List<MillingOrderInput>
            {
                new() { PaddyLotId = 1, LocationId = 1, ReservedWeightKg = 100m }
            }
        };

        _orderRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, object>>[]>()))
             .Returns(new List<Backend.Domain.Entities.MillingOrder> { order }.AsQueryable().BuildMock());

        var lot = new Backend.Domain.Entities.PaddyLot { Id = 1, LotCode = "LOT-PADDY-01", RemainingWeightKg = 200m, CostPricePerKg = 10m, ProductVariantId = 1, WarehouseId = 1, LocationId = 1 };
        _paddyLotRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lot);
        _paddyLotRepo.Setup(r => r.UpdateAsync(It.IsAny<Backend.Domain.Entities.PaddyLot>())).Returns(Task.CompletedTask);
        _paddyLotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _paddyLotRepo.Setup(r => r.CreateAsync(It.IsAny<Backend.Domain.Entities.PaddyLot>())).Returns(Task.CompletedTask);

        _paddyLotRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.PaddyLot, bool>>>(),
                It.IsAny<bool>()))
             .Returns(new List<Backend.Domain.Entities.PaddyLot>().AsQueryable().BuildMock());

        _statusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MillingOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<MillingOrderStatus, object>>[]>()))
             .ReturnsAsync((Expression<Func<MillingOrderStatus, bool>> expr, bool noTracking, Expression<Func<MillingOrderStatus, object>>[] includes) => {
                 var func = expr.Compile();
                 if (func(new MillingOrderStatus { Code = LookupCodes.MillingOrderStatus.Completed }))
                     return new MillingOrderStatus { Id = 5, Code = LookupCodes.MillingOrderStatus.Completed };
                 return new MillingOrderStatus { Id = 4, Code = LookupCodes.MillingOrderStatus.InProgress };
             });

        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LotStatus, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync(new LotStatus { Id = 1 });

        _orderRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);
        _locationRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Location, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync(new Location { Id = 1 });
        _locationRepo.Setup(r => r.UpdateCapacitySafetyAsync(
                It.IsAny<int>(), It.IsAny<int>(), It.IsAny<decimal>(),
                It.IsAny<int>(), It.IsAny<bool>(), It.IsAny<int>()))
            .ReturnsAsync(1);

        _invRepo.Setup(r => r.GetByVariantWarehouseLocationAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int?>(), It.IsAny<int?>()))
            .ReturnsAsync(new Backend.Domain.Entities.Inventory { Id = 1, QuantityOnHand = 100m, QuantityReserved = 100m });
        _invRepo.Setup(r => r.UpdateAsync(It.IsAny<Backend.Domain.Entities.Inventory>())).Returns(Task.CompletedTask);
        _invTxRepo.Setup(r => r.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);

        _outputRepo.Setup(r => r.CreateAsync(It.IsAny<MillingOrderOutput>())).Returns(Task.CompletedTask);
        _outputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _alertRepo.Setup(r => r.CreateAsync(It.IsAny<Alert>())).Returns(Task.CompletedTask);

        var dto = new CompleteMillingOrderDto
        {
            ActualYieldRate = 70m,
            LossKg = 4m,
            Outputs = new List<MillingOrderOutputItemDto>
            {
                new() { OutputWeightKg = 70m, OutputType = "RICE", IsByproduct = false, ProductVariantId = 1, LocationId = 1 },
                new() { OutputWeightKg = 25m, OutputType = "BYPRODUCT", IsByproduct = true, ProductVariantId = 2, LocationId = 1 }
            }
        };

        // Act
        var result = await Sut().CompleteMillingOrderAsync(1, dto, 1);

        // Assert
        result.Status.Should().Be(200, because: result.Message);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsSuccess_WithEmptyList()
    {
        _orderRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Backend.Domain.Entities.MillingOrder, object>>[]>()))
             .Returns(new List<Backend.Domain.Entities.MillingOrder>().AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task CreateListAsync_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreateMillingOrderDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task UpdateListAsync_Returns501()
    {
        var result = await Sut().UpdateListAsync(new List<UpdateMillingOrderDto>());
        result.Status.Should().Be(501);
    }
}
