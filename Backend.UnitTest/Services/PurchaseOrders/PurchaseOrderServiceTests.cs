using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.PurchaseOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
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
}
