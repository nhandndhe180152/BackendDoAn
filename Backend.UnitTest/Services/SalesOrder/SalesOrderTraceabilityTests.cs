using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.SalesOrders;
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
using MillingOrderEntity = Backend.Domain.Entities.MillingOrder;
using OrganizationEntity = Backend.Domain.Entities.Organization;
using CustomerFeedbackEntity = Backend.Domain.Entities.CustomerFeedback;

namespace Backend.UnitTest.Services.SalesOrders;

public class SalesOrderTraceabilityTests
{
    private readonly Mock<ISalesOrderRepository> _soRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderItem, int>> _soItemRepo = new();
    private readonly Mock<IRepositoryBase<SalesOrderStatus, int>> _soStatusRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrder, int>> _obRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItem, int>> _obItemRepo = new();
    private readonly Mock<IRepositoryBase<OutboundOrderStatus, int>> _obStatusRepo = new();
    private readonly Mock<IRepositoryBase<CustomerEntity, int>> _custRepo = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _invTxRepo = new();
    private readonly Mock<IPartyDebtRepository> _partyDebtRepo = new();
    private readonly Mock<IRepositoryBase<MillingOrderEntity, int>> _millingRepo = new();
    private readonly Mock<IRepositoryBase<OrganizationEntity, int>> _orgRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IApplicationDbContext> _dbContext = new();

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
        _orgRepo.Object,
        _http.Object,
        _dispatcher.Object,
        _dbContext.Object);

    [Fact]
    public async Task GetPagedAsync_PassesAllFilterParametersToRepository()
    {
        var fromDate = new DateTime(2026, 1, 1);
        var toDate = new DateTime(2026, 8, 20);
        var query = new SalesOrderPagedQuery
        {
            Keyword = "SO-2026",
            StatusId = 2,
            Channel = "WHOLESALE",
            CustomerId = 10,
            WarehouseId = 3,
            FromDate = fromDate,
            ToDate = toDate,
            Page = 2,
            PageSize = 15
        };

        _soRepo.Setup(r => r.CountAsync("SO-2026", 2, "WHOLESALE", 10, 3, fromDate, toDate))
            .ReturnsAsync(1);
        _soRepo.Setup(r => r.GetPagedListAsync("SO-2026", 15, 15, 2, "WHOLESALE", 10, 3, fromDate, toDate))
            .ReturnsAsync(new List<SalesOrder>());

        var result = await Sut().GetPagedAsync(query);

        result.Status.Should().Be(200);
        _soRepo.Verify(r => r.CountAsync("SO-2026", 2, "WHOLESALE", 10, 3, fromDate, toDate), Times.Once);
        _soRepo.Verify(r => r.GetPagedListAsync("SO-2026", 15, 15, 2, "WHOLESALE", 10, 3, fromDate, toDate), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOutboundSummaryAndFeedbacksWithoutNPlusOne()
    {
        var so = new SalesOrder
        {
            Id = 100,
            SOCode = "SO-100",
            CustomerId = 1,
            Customer = new CustomerEntity { Id = 1, Name = "Khách A" },
            StatusId = 5,
            Status = new SalesOrderStatus { Id = 5, Name = "Hoàn thành", Code = "COMPLETED" },
            WarehouseId = 2,
            Warehouse = new WarehouseEntity { Id = 2, Name = "Kho Chính" },
            TotalAmount = 5000000,
            IsDeleted = false,
            OutboundOrders = new List<OutboundOrder>
            {
                new()
                {
                    Id = 201,
                    SalesOrderId = 100,
                    WarehouseId = 2,
                    Warehouse = new WarehouseEntity { Id = 2, Name = "Kho Chính" },
                    OutboundOrderStatusId = 4,
                    OutboundOrderStatus = new OutboundOrderStatus { Id = 4, Name = "Đã xuất", Code = "DISPATCHED" },
                    TotalDispatchedSaleValue = 2500000,
                    IsDeleted = false
                },
                new()
                {
                    Id = 202,
                    SalesOrderId = 100,
                    WarehouseId = 2,
                    Warehouse = new WarehouseEntity { Id = 2, Name = "Kho Chính" },
                    OutboundOrderStatusId = 5,
                    OutboundOrderStatus = new OutboundOrderStatus { Id = 5, Name = "Hoàn thành", Code = "COMPLETED" },
                    TotalDispatchedSaleValue = 2500000,
                    IsDeleted = false
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
                Description = "Gạo tấm lẫn cám",
                ResolutionStatus = "OPEN",
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            },
            new()
            {
                Id = 2,
                SalesOrderId = 100,
                OutboundOrderId = 201,
                FeedbackType = "PACKAGING",
                Description = "Bao bị rách",
                ResolutionStatus = "RESOLVED",
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            },
            new()
            {
                Id = 3,
                SalesOrderId = 100,
                OutboundOrderId = 202,
                FeedbackType = "DELIVERY",
                Description = "Giao chậm 1 ngày",
                ResolutionStatus = "OPEN",
                CreatedDate = DateTime.UtcNow,
                IsDeleted = false
            }
        };

        _soRepo.Setup(r => r.GetByIdDetailAsync(100)).ReturnsAsync(so);
        _dbContext.Setup(c => c.CustomerFeedbacks).Returns(feedbacks.AsQueryable().BuildMockDbSet().Object);

        var result = await Sut().GetByIdAsync(100);

        result.Status.Should().Be(200);
        var detail = result.Resources as SalesOrderDetailDto;
        detail.Should().NotBeNull();
        detail!.OutboundCount.Should().Be(2);
        detail.FeedbackCount.Should().Be(3);
        detail.Feedbacks.Should().HaveCount(3);

        var ob201 = detail.OutboundOrders.FirstOrDefault(o => o.Id == 201);
        ob201.Should().NotBeNull();
        ob201!.FeedbackCount.Should().Be(2);

        var ob202 = detail.OutboundOrders.FirstOrDefault(o => o.Id == 202);
        ob202.Should().NotBeNull();
        ob202!.FeedbackCount.Should().Be(1);
    }
}
