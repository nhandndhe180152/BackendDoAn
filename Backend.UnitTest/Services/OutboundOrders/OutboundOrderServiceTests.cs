using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.OutboundOrders;

[Trait("Service", "OutboundOrder")]
public class OutboundOrderServiceTests
{
    private readonly Mock<IOutboundOrderRepository> _obRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderStatus, int>> _obStatusRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItemAllocation, int>> _allocRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<ISalesOrderRepository> _soRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderStatus, int>> _soStatusRepo = new();
    private readonly Mock<IPartyDebtRepository> _partyDebtRepo = new();
    private readonly Mock<IDebtTransactionRepository> _debtTxRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<IPaddyLotRepository> _paddyLotRepo = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private OutboundOrderService Sut() => new(
        _obRepo.Object, _obStatusRepo.Object, _allocRepo.Object, _invRepo.Object,
        _invTxRepo.Object, _soRepo.Object, _soStatusRepo.Object, _partyDebtRepo.Object,
        _debtTxRepo.Object, _http.Object, _paddyLotRepo.Object, _dispatcher.Object);

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync((OutboundOrder?)null);

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task CancelAsync_NonCancellableState_ReturnsConflict()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Đang giao hàng", Code = OutboundOrderStatusNames.Dispatched }
        };
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);

        var result = await Sut().CancelAsync(1);

        result.Status.Should().Be(409);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_DecreasesLocationOccupancy()
    {
        // Arrange
        var location = new Location { Id = 10, CurrentOccupancy = 50, SlotCode = "LOC-10" };
        var inventory = new Backend.Domain.Entities.Inventory { Id = 1, QuantityOnHand = 50, QuantityReserved = 10 };
        var allocation = new OutboundOrderItemAllocation
        {
            Id = 1,
            InventoryId = 1,
            LocationId = 10,
            Location = location,
            Inventory = inventory,
            QuantityAllocated = 10,
            QuantityPicked = 10,
            UnitCostPrice = 5
        };

        var item = new OutboundOrderItem
        {
            Id = 1,
            QuantityOrdered = 10,
            QuantityPicked = 10,
            Allocations = new List<OutboundOrderItemAllocation> { allocation }
        };

        var order = new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Name = "Đã đóng gói", Code = OutboundOrderStatusNames.Packed },
            OutboundOrderItems = new List<OutboundOrderItem> { item }
        };

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>().Object);
        
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<System.Linq.Expressions.Expression<System.Func<OutboundOrderStatus, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<System.Linq.Expressions.Expression<System.Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync(new OutboundOrderStatus { Id = 3, Name = "Đang giao hàng", Code = OutboundOrderStatusNames.Dispatched });

        // Act
        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto());

        // Assert
        result.Status.Should().Be(200);
        location.CurrentOccupancy.Should().Be(40); // 50 - 10 = 40
    }
}
