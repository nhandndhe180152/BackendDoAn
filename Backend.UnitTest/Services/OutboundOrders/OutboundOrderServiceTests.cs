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
using MockQueryable.Moq;
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
    private readonly Mock<Backend.Application.BackgroundJobs.DebtDueOverdue.IDebtAgingCalculationService> _aging = new();

    private OutboundOrderService Sut() => new(
        _obRepo.Object, _obStatusRepo.Object, _allocRepo.Object, _invRepo.Object,
        _invTxRepo.Object, _soRepo.Object, _soStatusRepo.Object, _partyDebtRepo.Object,
        _debtTxRepo.Object, _http.Object, _paddyLotRepo.Object, _dispatcher.Object, null, _aging.Object);

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
    public async Task ConfirmDispatchAsync_MissingDueDate_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(CreatePackedOrderWithReceivable());

        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto());

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmDispatchAsync_PastDueDate_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(CreatePackedOrderWithReceivable());

        var result = await Sut().ConfirmDispatchAsync(1, new Backend.Application.DTOs.OutboundOrders.ConfirmDispatchDto
        {
            DueDate = DateTime.Today.AddDays(-1)
        });

        result.Status.Should().Be(422);
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

    [Fact]
    public async Task CompleteDeliveryAsync_NegativePayment_ReturnsUnprocessableEntity()
    {
        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Dispatched }
        });

        var result = await Sut().CompleteDeliveryAsync(1, new Backend.Application.DTOs.OutboundOrders.CompleteDeliveryDto
        {
            ReceiverName = "Nguyễn Văn A",
            PaymentAmount = -1
        });

        result.Status.Should().Be(422);
        _obRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CompleteDeliveryAsync_PartialPayment_UpdatesDebtAndCreatesPayment()
    {
        var order = new OutboundOrder
        {
            Id = 1,
            SalesOrderId = 10,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Dispatched },
            OutboundOrderItems = new List<OutboundOrderItem>()
        };
        var debt = new Backend.Domain.Entities.PartyDebt { Id = 20, CurrentBalance = 100, IsActive = true };
        var charge = new DebtTransaction
        {
            Id = 30,
            PartyDebtId = 20,
            TransactionType = LookupCodes.DebtTransactionType.Charge,
            Amount = 100,
            RefType = "OUTBOUND_ORDER",
            RefId = 1
        };
        var dbTransaction = new Mock<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>();

        _obRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obRepo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(dbTransaction.Object);
        _debtTxRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, object>>[]>()!))
            .ReturnsAsync(charge);
        _partyDebtRepo.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(debt);
        _debtTxRepo.Setup(r => r.FindByCondition(
                It.IsAny<System.Linq.Expressions.Expression<Func<DebtTransaction, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<DebtTransaction> { charge }.AsQueryable().BuildMock());
        _aging.Setup(x => x.CalculateDebtDocuments(debt, It.IsAny<List<DebtTransaction>>()))
            .Returns(new List<Backend.Application.BackgroundJobs.DebtDueOverdue.DebtDocumentAllocation>
            {
                new() { RefType = "OUTBOUND_ORDER", RefId = 1, OutstandingAmount = 100 }
            });
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrderStatus, object>>[]>()!))
            .ReturnsAsync(new OutboundOrderStatus { Id = 4, Code = OutboundOrderStatusNames.Completed });
        _obRepo.Setup(r => r.FindByCondition(
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<OutboundOrder, object>>[]>()!))
            .Returns(new List<OutboundOrder> { order }.AsQueryable().BuildMock());
        _soRepo.Setup(r => r.GetByIdDetailAsync(10)).ReturnsAsync(new SalesOrder
        {
            Id = 10,
            SalesOrderItems = new List<SalesOrderItem>()
        });
        _soStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<SalesOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<System.Linq.Expressions.Expression<Func<SalesOrderStatus, object>>[]>()!))
            .ReturnsAsync(new SalesOrderStatus { Id = 5, Code = SalesOrderStatusNames.Completed });

        var result = await Sut().CompleteDeliveryAsync(1, new Backend.Application.DTOs.OutboundOrders.CompleteDeliveryDto
        {
            ReceiverName = "Nguyễn Văn A",
            PaymentAmount = 40
        });

        result.Status.Should().Be(200);
        debt.CurrentBalance.Should().Be(60);
        _debtTxRepo.Verify(r => r.CreateAsync(It.Is<DebtTransaction>(x =>
            x.TransactionType == LookupCodes.DebtTransactionType.Payment &&
            x.Amount == 40 && x.RefType == "OUTBOUND_ORDER" && x.RefId == 1)), Times.Once);
        dbTransaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static OutboundOrder CreatePackedOrderWithReceivable()
    {
        return new OutboundOrder
        {
            Id = 1,
            OutboundOrderStatus = new OutboundOrderStatus { Code = OutboundOrderStatusNames.Packed },
            OutboundOrderItems = new List<OutboundOrderItem>
            {
                new()
                {
                    QuantityOrdered = 1,
                    SalesOrderItem = new SalesOrderItem
                    {
                        QuantityOrdered = 1,
                        LineAmount = 100
                    },
                    Allocations = new List<OutboundOrderItemAllocation>
                    {
                        new() { QuantityPicked = 1 }
                    }
                }
            }
        };
    }
}
