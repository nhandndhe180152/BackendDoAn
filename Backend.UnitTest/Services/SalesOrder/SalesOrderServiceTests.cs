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
using Microsoft.EntityFrameworkCore.Storage;
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
    private readonly Mock<IRepositoryBase<global::Backend.Domain.Entities.Organization, int>> _orgRepo = new();
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
        _orgRepo.Object,
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
            Status = new SalesOrderStatus { Name = "Chờ xác nhận", Code = SalesOrderStatusNames.PendingConfirm },
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
            Status = new SalesOrderStatus { Name = "Mới tạo", Code = SalesOrderStatusNames.New },
            SalesOrderItems = new List<SalesOrderItem>()
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        var result = await Sut().ReserveAsync(1);

        result.Status.Should().Be(409);
    }

    [Fact]
    public async Task ReserveAsync_InsufficientStock_ReturnsProductNameAndFormattedKg()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            CustomerId = 2,
            WarehouseId = 3,
            Status = new SalesOrderStatus
            {
                Name = "Chờ xác nhận",
                Code = SalesOrderStatusNames.PendingConfirm
            },
            SalesOrderItems = new List<SalesOrderItem>
            {
                new()
                {
                    ProductVariantId = 132,
                    ProductVariant = new ProductVariant { Name = "Gạo BC15 đóng bao 10kg" },
                    QuantityOrdered = 20.000m
                }
            }
        };

        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);
        _custRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(new global::Backend.Domain.Entities.Customer
        {
            Id = 2,
            IsActive = true
        });
        _invRepo.Setup(r => r.GetAvailableForSalesAsync(132, 3))
            .ReturnsAsync(new List<global::Backend.Domain.Entities.Inventory>());
        _soRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);

        var result = await Sut().ReserveAsync(1);

        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.SalesOrder.InsufficientStock);
        result.Message.Should().Be(
            "Tồn khả dụng không đủ cho sản phẩm Gạo BC15 đóng bao 10kg. Cần: 20 kg, Khả dụng: 0 kg.");
        result.Message.Should().NotContain("ID 132");
    }

    [Fact]
    public async Task CreateAsync_DuplicateItems_ReturnsBadRequest()
    {
        var dto = new CreateSalesOrderDto
        {
            CustomerId = 1,
            Items = new List<CreateSalesOrderItemDto>
            {
                new() { ProductVariantId = 102, QuantityOrdered = 100 },
                new() { ProductVariantId = 102, QuantityOrdered = 50 }
            }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("trùng");
    }

    [Fact]
    public async Task CreateAsync_DepositGreaterThanCalculatedTotal_ReturnsBadRequest()
    {
        var dto = new CreateSalesOrderDto
        {
            CustomerId = 1,
            DepositAmount = 101m,
            Items = new List<CreateSalesOrderItemDto>
            {
                new() { ProductVariantId = 102, QuantityOrdered = 1m, UnitSalePrice = 100m }
            }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("Tiền cọc");
        _soRepo.Verify(r => r.CreateAsync(It.IsAny<global::Backend.Domain.Entities.SalesOrder>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateItems_ReturnsBadRequest()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            Status = new SalesOrderStatus { Name = "Mới tạo", Code = SalesOrderStatusNames.New }
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        var dto = new UpdateSalesOrderDto
        {
            Id = 1,
            Items = new List<UpdateSalesOrderItemDto>
            {
                new() { ProductVariantId = 102, QuantityOrdered = 100 },
                new() { ProductVariantId = 102, QuantityOrdered = 50 }
            }
        };

        var result = await Sut().UpdateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("trùng");
    }

    [Fact]
    public async Task UpdateAsync_DepositGreaterThanRecalculatedTotal_ReturnsBadRequest()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            TotalAmount = 500m,
            Status = new SalesOrderStatus { Name = "Mới tạo", Code = SalesOrderStatusNames.New }
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        var result = await Sut().UpdateAsync(new UpdateSalesOrderDto
        {
            Id = 1,
            DepositAmount = 101m,
            Items = new List<UpdateSalesOrderItemDto>
            {
                new() { ProductVariantId = 102, QuantityOrdered = 1m, UnitSalePrice = 100m }
            }
        });

        result.Status.Should().Be(400);
        result.Message.Should().Contain("Tiền cọc");
        _soRepo.Verify(r => r.UpdateAsync(It.IsAny<global::Backend.Domain.Entities.SalesOrder>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_NonCancellableState_ReturnsConflict()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            SOCode = "SO-1",
            Status = new SalesOrderStatus { Name = "Đang giao", Code = SalesOrderStatusNames.Delivering }
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);

        // Có lý do hợp lệ nhưng trạng thái không cho hủy → vẫn phải 409.
        var result = await Sut().CancelAsync(1, "Khách hủy đặt hàng");

        result.Status.Should().Be(409);
        so.CancelReason.Should().BeNull();
    }

    [Fact]
    public async Task CancelAsync_NewOrder_SavesCancelReason()
    {
        var so = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            SOCode = "SO-1",
            Status = new SalesOrderStatus { Id = 1, Name = "Mới tạo", Code = SalesOrderStatusNames.New },
            SalesOrderItems = new List<SalesOrderItem>()
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(so);
        _soRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);
        // Đơn chưa có phiếu xuất nào.
        _obRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<OutboundOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrder, object>>[]>()))
            .Returns(new List<OutboundOrder>().AsQueryable().BuildMock());
        _soStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SalesOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<SalesOrderStatus, object>>[]>()))
            .ReturnsAsync(new SalesOrderStatus { Id = 8, Name = "Đã hủy", Code = SalesOrderStatusNames.Cancelled });
        _dispatcher
            .Setup(x => x.DispatchAsync(
                It.IsAny<string>(),
                It.IsAny<NotificationTarget>(),
                It.IsAny<object[]?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>(),
                It.IsAny<string?>(),
                It.IsAny<int?>()))
            .Returns(Task.CompletedTask);

        var result = await Sut().CancelAsync(1, "  Khách hủy đặt hàng  ");

        result.Status.Should().Be(200);
        so.StatusId.Should().Be(8);
        // Lý do được trim trước khi lưu.
        so.CancelReason.Should().Be("Khách hủy đặt hàng");
    }

    [Fact]
    public async Task CreateOutboundAsync_LocalizedNames_UsesStatusCodes()
    {
        var order = new global::Backend.Domain.Entities.SalesOrder
        {
            Id = 1,
            SOCode = "SO-1",
            WarehouseId = 1,
            Status = new SalesOrderStatus { Id = 3, Name = "Đã giữ hàng", Code = SalesOrderStatusNames.Reserved },
            SalesOrderItems = new List<SalesOrderItem>
            {
                new() { Id = 10, ProductVariantId = 5, QuantityOrdered = 10 }
            }
        };
        _soRepo.Setup(r => r.GetByIdDetailAsync(1)).ReturnsAsync(order);
        _obStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<OutboundOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrderStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<OutboundOrderStatus, bool>> predicate, bool _, Expression<Func<OutboundOrderStatus, object>>[] __) =>
                predicate.Compile()(new OutboundOrderStatus { Id = 1, Name = "Nháp", Code = OutboundOrderStatusNames.Draft })
                    ? new OutboundOrderStatus { Id = 1, Name = "Nháp", Code = OutboundOrderStatusNames.Draft }
                    : null);
        _soStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<SalesOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<SalesOrderStatus, object>>[]>()))
            .ReturnsAsync(new SalesOrderStatus { Id = 5, Name = "Đang chuẩn bị", Code = SalesOrderStatusNames.Preparing });
        _obRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<OutboundOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<OutboundOrder, object>>[]>()))
            .Returns(new List<OutboundOrder>().AsQueryable().BuildMock());
        _soRepo.Setup(r => r.BeginTransactionAsync())
            .ReturnsAsync(new Mock<IDbContextTransaction>().Object);

        var result = await Sut().CreateOutboundAsync(1, new CreateOutboundDto
        {
            Items = new List<CreateOutboundItemDto>
            {
                new() { ProductVariantId = 5, QuantityToDispatch = 5 }
            }
        });

        result.Status.Should().Be(201);
        order.StatusId.Should().Be(5);
        _obRepo.Verify(r => r.CreateAsync(It.Is<OutboundOrder>(x =>
            x.OutboundOrderStatusId == 1 && x.OutboundOrderItems.Count == 1)), Times.Once);
    }
}

