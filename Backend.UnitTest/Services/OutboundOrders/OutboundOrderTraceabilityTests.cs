using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.OutboundOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MockQueryable.Moq;
using Moq;
using Xunit;
using WarehouseEntity = Backend.Domain.Entities.Warehouse;
using CustomerEntity = Backend.Domain.Entities.Customer;
using PaddyLotEntity = Backend.Domain.Entities.PaddyLot;
using CustomerFeedbackEntity = Backend.Domain.Entities.CustomerFeedback;

namespace Backend.UnitTest.Services.OutboundOrders;

public class OutboundOrderTraceabilityTests
{
    private readonly Mock<IOutboundOrderRepository> _outboundOrderRepository = new();
    private readonly Mock<IRepositoryBase<OutboundOrderStatus, int>> _outboundStatusRepository = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItemAllocation, int>> _allocationRepository = new();
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IInventoryTransactionRepository> _inventoryTransactionRepository = new();
    private readonly Mock<ISalesOrderRepository> _salesOrderRepository = new();
    private readonly Mock<IRepositoryBase<SalesOrderStatus, int>> _salesOrderStatusRepository = new();
    private readonly Mock<IPartyDebtRepository> _partyDebtRepository = new();
    private readonly Mock<IDebtTransactionRepository> _debtTransactionRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly Mock<IPaddyLotRepository> _paddyLotRepository = new();
    private readonly Mock<INotificationDispatcher> _notificationDispatcher = new();
    private readonly Mock<IRepositoryBase<PaddyLotBagAllocation, int>> _bagAllocationRepository = new();
    private readonly Mock<IApplicationDbContext> _dbContext = new();

    private OutboundOrderService Sut() => new(
        _outboundOrderRepository.Object,
        _outboundStatusRepository.Object,
        _allocationRepository.Object,
        _inventoryRepository.Object,
        _inventoryTransactionRepository.Object,
        _salesOrderRepository.Object,
        _salesOrderStatusRepository.Object,
        _partyDebtRepository.Object,
        _debtTransactionRepository.Object,
        _httpContextAccessor.Object,
        _paddyLotRepository.Object,
        _notificationDispatcher.Object,
        bagAllocationRepository: _bagAllocationRepository.Object,
        dbContext: _dbContext.Object);

    [Fact]
    public async Task GetPagedAsync_PassesAllFilterParametersToRepository()
    {
        var fromDate = new DateTime(2026, 2, 1);
        var toDate = new DateTime(2026, 8, 20);
        var query = new OutboundOrderPagedQuery
        {
            Keyword = "201",
            OutboundStatusId = 4,
            SalesOrderId = 100,
            WarehouseId = 2,
            FromDate = fromDate,
            ToDate = toDate,
            Page = 1,
            PageSize = 20
        };

        _outboundOrderRepository.Setup(r => r.CountAsync("201", 4, 100, 2, fromDate, toDate))
            .ReturnsAsync(1);
        _outboundOrderRepository.Setup(r => r.GetPagedListAsync("201", 0, 20, 4, 100, 2, fromDate, toDate))
            .ReturnsAsync(new List<OutboundOrder>());

        var result = await Sut().GetPagedAsync(query);

        result.Status.Should().Be(200);
        _outboundOrderRepository.Verify(r => r.CountAsync("201", 4, 100, 2, fromDate, toDate), Times.Once);
        _outboundOrderRepository.Verify(r => r.GetPagedListAsync("201", 0, 20, 4, 100, 2, fromDate, toDate), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsBagAllocationsAndFeedbacks()
    {
        var outbound = new OutboundOrder
        {
            Id = 201,
            SalesOrderId = 100,
            SalesOrder = new SalesOrder
            {
                Id = 100,
                SOCode = "SO-100",
                CustomerId = 1,
                Customer = new CustomerEntity { Id = 1, Name = "Khách Hàng A" }
            },
            WarehouseId = 2,
            Warehouse = new WarehouseEntity { Id = 2, Name = "Kho Long An" },
            OutboundOrderStatusId = 4,
            OutboundOrderStatus = new OutboundOrderStatus { Id = 4, Name = "Đã xuất", Code = "DISPATCHED" },
            TotalDispatchedSaleValue = 3000000,
            IsDeleted = false
        };

        var bagAllocs = new List<PaddyLotBagAllocation>
        {
            new()
            {
                Id = 10,
                ReferenceType = "OUTBOUND_ORDER",
                ReferenceId = 201,
                BagId = 101,
                AllocatedWeightKg = 50,
                PickedWeightKg = 50,
                Status = "CONSUMED",
                IsDeleted = false,
                Bag = new PaddyLotBag
                {
                    Id = 101,
                    BagNo = 1,
                    LotId = 50,
                    Lot = new PaddyLotEntity { Id = 50, LotCode = "LOT-50" },
                    LocationId = 5,
                    Location = new Location { Id = 5, ZoneName = "A", ShelfRow = "1", SlotCode = "A-1-01" },
                    QrCode = "BAG-001"
                }
            }
        };

        var feedbacks = new List<CustomerFeedbackEntity>
        {
            new()
            {
                Id = 1,
                SalesOrderId = 100,
                OutboundOrderId = 201,
                FeedbackType = "QUALITY",
                Description = "Gạo bị ẩm",
                ResolutionStatus = "OPEN",
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            }
        };

        _outboundOrderRepository.Setup(r => r.GetByIdDetailAsync(201)).ReturnsAsync(outbound);
        _bagAllocationRepository.Setup(r => r.FindByCondition(
                It.IsAny<System.Linq.Expressions.Expression<Func<PaddyLotBagAllocation, bool>>>(),
                false))
            .Returns(bagAllocs.AsQueryable().BuildMock());
        _dbContext.Setup(c => c.CustomerFeedbacks).Returns(feedbacks.AsQueryable().BuildMockDbSet().Object);

        var result = await Sut().GetByIdAsync(201);

        result.Status.Should().Be(200);
        var detail = result.Resources as OutboundOrderDetailDto;
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(201);
        detail.SalesOrderId.Should().Be(100);
        detail.SOCode.Should().Be("SO-100");
        detail.CustomerName.Should().Be("Khách Hàng A");
        detail.BagAllocations.Should().HaveCount(1);
        detail.BagAllocations[0].Status.Should().Be("CONSUMED");
        detail.FeedbackCount.Should().Be(1);
        detail.Feedbacks.Should().HaveCount(1);
        detail.Feedbacks[0].FeedbackType.Should().Be("QUALITY");
    }
}
