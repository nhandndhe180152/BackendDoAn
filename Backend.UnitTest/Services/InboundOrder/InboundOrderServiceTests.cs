using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.InboundOrder;

/// <summary>
/// Test cho endpoint mới GetPutawayPendingAsync (màn Store-in) và refactor N+1 của GetByIdAsync.
/// Dùng EF Core InMemory + repository thật (vì có Include/ThenInclude), mock các dependency không dùng.
/// </summary>
public class InboundOrderServiceTests
{
    // ── Status IDs seed ───────────────────────────────────────────────
    private const int StatusApprovedId = 1;
    private const int StatusConfirmedId = 2;
    private const int StatusDraftId = 3;

    private const int VariantId = 105;
    private const int LotId = 35;

    private static BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private static Backend.Application.Implements.InboundOrderService BuildService(BackendContext context)
    {
        var uow = Mock.Of<IUnitOfWork>();
        var inboundOrderRepo = new RepositoryBase<Backend.Domain.Entities.InboundOrder, int>(context, uow);
        var productVariantRepo = new ProductVariantRepository(context, uow);
        var paddyLotRepo = new RepositoryBase<Backend.Domain.Entities.PaddyLot, int>(context, uow);

        return new Backend.Application.Implements.InboundOrderService(
            inboundOrderRepo,
            Mock.Of<IInboundOrderItemRepository>(),
            Mock.Of<IRepositoryBase<InboundOrderStatus, int>>(),
            Mock.Of<IWarehouseRepository>(),
            Mock.Of<IRepositoryBase<Backend.Domain.Entities.Supplier, int>>(),
            productVariantRepo,
            Mock.Of<ILocationRepository>(),
            Mock.Of<IInventoryRepository>(),
            Mock.Of<IInventoryTransactionRepository>(),
            Mock.Of<ISystemConfigRepository>(),
            Mock.Of<IRepositoryBase<DeliveryNote, int>>(),
            Mock.Of<IRepositoryBase<FileUpload, int>>(),
            Mock.Of<IStorageService>(),
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<ILogger<Backend.Application.Implements.InboundOrderService>>(),
            Mock.Of<INotificationDispatcher>(),
            Mock.Of<IRepositoryBase<PaddyPurchaseReceipt, int>>(),
            Mock.Of<IRepositoryBase<Backend.Domain.Entities.PaddyPurchaseSchedule, int>>(),
            Mock.Of<ISystemLookup>(),
            Mock.Of<IRepositoryBase<PurchaseOrder, int>>(),
            Mock.Of<IRepositoryBase<PurchaseOrderStatus, int>>(),
            paddyLotRepo,
            Mock.Of<IRepositoryBase<LotStatus, int>>(),
            Mock.Of<IPaddyPurchaseReceiptService>());
    }

    private static async Task SeedMasterDataAsync(BackendContext context)
    {
        context.InboundOrderStatuses.AddRange(
            new InboundOrderStatus { Id = StatusApprovedId, Name = "Đã duyệt", Code = InboundOrderStatusNames.Approved, Color = "#000" },
            new InboundOrderStatus { Id = StatusConfirmedId, Name = "Đã nhận hàng", Code = InboundOrderStatusNames.Confirmed, Color = "#000" },
            new InboundOrderStatus { Id = StatusDraftId, Name = "Nháp", Code = InboundOrderStatusNames.Draft, Color = "#000" });

        context.Warehouses.Add(new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH-01", Name = "Kho 1", IsActive = true });
        context.Suppliers.Add(new Backend.Domain.Entities.Supplier { Id = 1, Code = "SUP-01", Name = "NCC 1", IsActive = true });
        context.Farmers.Add(new Backend.Domain.Entities.Farmer { Id = 1, Code = "F-01", Name = "Nông dân A" });
        context.PaddyPurchaseReceipts.Add(new PaddyPurchaseReceipt
        {
            Id = 1,
            ReceiptCode = "PPR-1",
            FarmerId = 1,
            WarehouseId = 1,
            ActualWeightKg = 3000,
            ReceiptDate = DateTime.UtcNow
        });

        context.Products.Add(new Backend.Domain.Entities.Product { Id = 1, Name = "Lúa nguyên liệu", ProductCategoryId = 1, IsActive = true });
        context.ProductVariants.Add(new ProductVariant
        {
            Id = VariantId,
            Name = "Lúa OM5451",
            SKU = "SKU-105",
            ProductId = 1,
            UnitOfMeasureId = 1,
            IsActive = true
        });

        context.LotStatuses.Add(new LotStatus { Id = 1, Code = "IN_STOCK", Name = "Trong kho", Color = "#10B981" });
        context.PaddyLots.Add(new Backend.Domain.Entities.PaddyLot
        {
            Id = LotId,
            LotCode = "LOT-35",
            LotType = "PADDY",
            ProductVariantId = VariantId,
            StatusId = 1,
            WarehouseId = 1,
            QualityStatus = "PASSED",
            CostPricePerKg = 6500,
            CreatedDate = DateTime.UtcNow
        });

        await context.SaveChangesAsync();
    }

    private static Backend.Domain.Entities.InboundOrder MakePaddyOrder(
        int id, int statusId, DateTime createdDate, string sourceType, int? receiptId)
    {
        return new Backend.Domain.Entities.InboundOrder
        {
            Id = id,
            POCode = $"PO-{id}",
            WarehouseId = 1,
            InboundOrderStatusId = statusId,
            SourceType = sourceType,
            PaddyPurchaseReceiptId = receiptId,
            CreatedDate = createdDate,
            InboundOrderItems = new List<InboundOrderItem>
            {
                new()
                {
                    Id = id * 100,
                    ProductVariantId = VariantId,
                    PaddyLotId = LotId,
                    QuantityOrdered = 3000,
                    QuantityReceived = 0,
                    CreatedDate = createdDate
                }
            }
        };
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task GetPutawayPendingAsync_ChiTraVePhieuLuaGaoDangChoXepKho_LocDungTrangThai()
    {
        using var context = CreateContext();
        await SeedMasterDataAsync(context);

        var baseTime = new DateTime(2026, 07, 29, 8, 0, 0, DateTimeKind.Utc);
        context.InboundOrders.AddRange(
            // Hợp lệ: RECEIPT + Approved (mới hơn)
            MakePaddyOrder(10, StatusApprovedId, baseTime.AddMinutes(10), "RECEIPT", 1),
            // Loại: Confirmed (đã hoàn tất)
            MakePaddyOrder(11, StatusConfirmedId, baseTime.AddMinutes(9), "RECEIPT", 1),
            // Loại: nguồn PO (không phải lúa/gạo)
            MakePaddyOrder(12, StatusApprovedId, baseTime.AddMinutes(8), "PO", null),
            // Hợp lệ: PADDY_PURCHASE + Draft (cũ hơn)
            MakePaddyOrder(13, StatusDraftId, baseTime.AddMinutes(5), "PADDY_PURCHASE", null));
        await context.SaveChangesAsync();

        var service = BuildService(context);

        var result = await service.GetPutawayPendingAsync();

        result.IsSucceeded.Should().BeTrue();
        var data = result.Resources.Should().BeOfType<List<InboundOrderDetailDto>>().Subject;

        // Chỉ còn phiếu 10 và 13; loại 11 (Confirmed) và 12 (PO)
        data.Select(x => x.Id).Should().BeEquivalentTo(new[] { 10, 13 });
        // Sắp xếp CreatedDate giảm dần: 10 trước 13
        data.Select(x => x.Id).Should().ContainInOrder(10, 13);
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task GetPutawayPendingAsync_HydrateItem_GanTenVariantVaThongTinLot()
    {
        using var context = CreateContext();
        await SeedMasterDataAsync(context);

        context.InboundOrders.Add(
            MakePaddyOrder(10, StatusApprovedId, DateTime.UtcNow, "RECEIPT", 1));
        await context.SaveChangesAsync();

        var service = BuildService(context);

        var result = await service.GetPutawayPendingAsync();

        var data = result.Resources.Should().BeOfType<List<InboundOrderDetailDto>>().Subject;
        var order = data.Single();
        order.Items.Should().HaveCount(1);

        var item = order.Items.Single();
        item.ProductVariantName.Should().Be("Lúa OM5451");
        item.SKU.Should().Be("SKU-105");
        item.PaddyLotCode.Should().Be("LOT-35");
        item.PaddyQualityStatus.Should().Be("PASSED");
        // Phiếu nguồn RECEIPT => tên NCC lấy từ nông dân của phiếu thu mua
        order.SupplierName.Should().Be("Nông dân A");
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task GetPutawayPendingAsync_KhongCoPhieuLuaGao_TraVeDanhSachRong()
    {
        using var context = CreateContext();
        await SeedMasterDataAsync(context);

        // Chỉ có phiếu nguồn PO -> không thuộc luồng put-away lúa/gạo
        context.InboundOrders.Add(
            MakePaddyOrder(20, StatusApprovedId, DateTime.UtcNow, "PO", null));
        await context.SaveChangesAsync();

        var service = BuildService(context);

        var result = await service.GetPutawayPendingAsync();

        result.IsSucceeded.Should().BeTrue();
        var data = result.Resources.Should().BeOfType<List<InboundOrderDetailDto>>().Subject;
        data.Should().BeEmpty();
    }

    [Fact]
    [Trait("Service", "InboundOrder")]
    public async Task GetByIdAsync_HydrateItemKhongN1_GanDuVariantVaLot()
    {
        using var context = CreateContext();
        await SeedMasterDataAsync(context);

        context.InboundOrders.Add(
            MakePaddyOrder(10, StatusApprovedId, DateTime.UtcNow, "RECEIPT", 1));
        await context.SaveChangesAsync();

        var service = BuildService(context);

        var result = await service.GetByIdAsync(10);

        result.IsSucceeded.Should().BeTrue();
        var detail = result.Resources.Should().BeOfType<InboundOrderDetailDto>().Subject;
        var item = detail.Items.Single();
        item.ProductVariantName.Should().Be("Lúa OM5451");
        item.PaddyLotCode.Should().Be("LOT-35");
    }
}
