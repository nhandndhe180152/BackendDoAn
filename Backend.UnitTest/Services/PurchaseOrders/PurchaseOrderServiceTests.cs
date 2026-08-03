using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.PurchaseOrders;
using Backend.Application.Constants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.PurchaseOrders;

[Trait("Service", "PurchaseOrder")]
public class PurchaseOrderServiceTests
{
    private readonly Mock<IRepositoryBase<PurchaseOrder, int>> _poRepo = new();
    private readonly Mock<IRepositoryBase<PurchaseOrderItem, int>> _poItemRepo = new();
    private readonly Mock<IRepositoryBase<PurchaseOrderStatus, int>> _poStatusRepo = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.InboundOrder, int>> _inboundRepo = new();
    private readonly Mock<IRepositoryBase<InboundOrderItem, int>> _inboundItemRepo = new();
    private readonly Mock<IRepositoryBase<InboundOrderStatus, int>> _inboundStatusRepo = new();
    private readonly Mock<IRepositoryBase<global::Backend.Domain.Entities.Supplier, int>> _supplierRepo = new();
    private readonly Mock<IRepositoryBase<ProductVariant, int>> _variantRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private PurchaseOrderService Sut() => new(
        _poRepo.Object, _poItemRepo.Object, _poStatusRepo.Object, _inboundRepo.Object,
        _inboundItemRepo.Object, _inboundStatusRepo.Object, _supplierRepo.Object,
        _variantRepo.Object, _http.Object, _dispatcher.Object);

    [Fact]
    public async Task CreateAsync_NoItems_ReturnsBadRequest()
    {
        var dto = new CreatePurchaseOrderDto { SupplierId = 1, Items = new List<CreatePurchaseOrderItemDto>() };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_SupplierNotFound_ReturnsUnprocessable()
    {
        _supplierRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.Supplier, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<global::Backend.Domain.Entities.Supplier, object>>[]>()))
            .ReturnsAsync((global::Backend.Domain.Entities.Supplier?)null);

        var dto = new CreatePurchaseOrderDto
        {
            SupplierId = 999,
            Items = new List<CreatePurchaseOrderItemDto> { new() }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task ConfirmAsync_LocalizedName_ResolvesNextStatusByCode()
    {
        var order = new PurchaseOrder
        {
            Id = 1,
            POCode = "PO-1",
            Status = new PurchaseOrderStatus { Id = 1, Name = "Nháp", Code = PurchaseOrderStatusNames.Draft },
            PurchaseOrderItems = new List<PurchaseOrderItem>()
        };
        _poRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<PurchaseOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PurchaseOrder, object>>[]>()))
            .Returns(new[] { order }.AsQueryable().BuildMock());
        _poStatusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<PurchaseOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<PurchaseOrderStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<PurchaseOrderStatus, bool>> predicate, bool _, Expression<Func<PurchaseOrderStatus, object>>[] __) =>
                predicate.Compile()(new PurchaseOrderStatus { Id = 2, Name = "Đã xác nhận", Code = PurchaseOrderStatusNames.Confirmed })
                    ? new PurchaseOrderStatus { Id = 2, Name = "Đã xác nhận", Code = PurchaseOrderStatusNames.Confirmed }
                    : null);
        _dispatcher.Setup(d => d.DispatchAsync(
                It.IsAny<string>(), It.IsAny<NotificationTarget>(), It.IsAny<object[]?>(),
                It.IsAny<string?>(), It.IsAny<int?>(), It.IsAny<string?>(), It.IsAny<int?>()))
            .Returns(Task.CompletedTask);

        var result = await Sut().ConfirmAsync(1);

        result.Status.Should().Be(200);
        order.StatusId.Should().Be(2);
    }
}
