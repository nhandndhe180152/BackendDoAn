using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.SalesOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.SalesOrders;

[Trait("Service", "SalesOrder")]
public class SalesOrderServiceTests
{
    private readonly Mock<ISalesOrderRepository> _soRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderItem, int>> _soItemRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderStatus, int>> _soStatusRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrder, int>> _obRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItem, int>> _obItemRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderStatus, int>> _obStatusRepo = new();
    private readonly Mock<IRepositoryBase<global::Backend.Domain.Entities.Customer, int>> _custRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<IPartyDebtRepository> _partyDebtRepo = new();
    private readonly Mock<IRepositoryBase<global::Backend.Domain.Entities.MillingOrder, int>> _millingRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private SalesOrderService Sut() => new(
        _soRepo.Object,
        _soItemRepo.Object,
        _soStatusRepo.Object,
        _obRepo.Object,
        _obItemRepo.Object,
        _obStatusRepo.Object,
        _custRepo.Object,
        _invRepo.Object,
        _invTxRepo.Object,
        _partyDebtRepo.Object,
        _millingRepo.Object,
        _http.Object,
        _dispatcher.Object);

    [Fact]
    public async Task CreateAsync_NoItems_ReturnsBadRequest()
    {
        var dto = new CreateSalesOrderDto { CustomerId = 1, Items = new List<CreateSalesOrderItemDto>() };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task ReserveAsync_RequiresMilling_NoCompletedMilling_ReturnsUnprocessable()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            RequiresMilling = true,
            WarehouseId = 1,
            Status = new SalesOrderStatus { Name = SalesOrderStatusNames.PendingConfirm },
            SalesOrderItems = new List<SalesOrderItem>()
        };

        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        // Không có lệnh xay COMPLETED nào gắn với đơn → AnyAsync trả false → chặn (Gap 2)
        _millingRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.MillingOrder, object>>[]>()))
            .Returns(new List<global::Backend.Domain.Entities.MillingOrder>().AsQueryable().BuildMock());

        var result = await Sut().ReserveAsync(1);

        result.Status.Should().Be(422);
        result.Message.Should().Contain("xay");
    }

    [Fact]
    public async Task ReserveAsync_WrongStatus_ReturnsConflict()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            Status = new SalesOrderStatus { Name = SalesOrderStatusNames.New },
            SalesOrderItems = new List<SalesOrderItem>()
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        var result = await Sut().ReserveAsync(1);

        result.Status.Should().Be(409);
    }
}
