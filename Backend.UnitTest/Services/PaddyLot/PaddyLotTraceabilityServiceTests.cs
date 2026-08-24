using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.Implements;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using OrganizationEntity = Backend.Domain.Entities.Organization;
using WarehouseEntity = Backend.Domain.Entities.Warehouse;
using RiceVarietyEntity = Backend.Domain.Entities.RiceVariety;
using FarmerEntity = Backend.Domain.Entities.Farmer;
using CustomerEntity = Backend.Domain.Entities.Customer;
using UserEntity = Backend.Domain.Entities.User;
using PaddyLotEntity = Backend.Domain.Entities.PaddyLot;
using QualityInspectionEntity = Backend.Domain.Entities.QualityInspection;
using MillingOrderEntity = Backend.Domain.Entities.MillingOrder;
using MillingOrderInputEntity = Backend.Domain.Entities.MillingOrderInput;
using MillingOrderOutputEntity = Backend.Domain.Entities.MillingOrderOutput;
using LotStatusEntity = Backend.Domain.Entities.LotStatus;
using ProductVariantEntity = Backend.Domain.Entities.ProductVariant;
using ProductCategoryEntity = Backend.Domain.Entities.ProductCategory;
using ProductEntity = Backend.Domain.Entities.Product;
using UnitOfMeasureEntity = Backend.Domain.Entities.UnitOfMeasure;
using LocationEntity = Backend.Domain.Entities.Location;
using OutboundOrderStatusEntity = Backend.Domain.Entities.OutboundOrderStatus;
using SalesOrderStatusEntity = Backend.Domain.Entities.SalesOrderStatus;
using MillingOrderStatusEntity = Backend.Domain.Entities.MillingOrderStatus;
using PaddyPurchaseReceiptEntity = Backend.Domain.Entities.PaddyPurchaseReceipt;
using SalesOrderEntity = Backend.Domain.Entities.SalesOrder;
using OutboundOrderEntity = Backend.Domain.Entities.OutboundOrder;
using OutboundOrderItemEntity = Backend.Domain.Entities.OutboundOrderItem;
using OutboundOrderItemAllocationEntity = Backend.Domain.Entities.OutboundOrderItemAllocation;
using InventoryEntity = Backend.Domain.Entities.Inventory;
using CustomerFeedbackEntity = Backend.Domain.Entities.CustomerFeedback;
using CustomerReturnOrderEntity = Backend.Domain.Entities.CustomerReturnOrder;
using CustomerReturnOrderItemEntity = Backend.Domain.Entities.CustomerReturnOrderItem;
using CustomerReturnAllocationEntity = Backend.Domain.Entities.CustomerReturnOrderItemAllocation;
using CustomerReturnStatusEntity = Backend.Domain.Entities.CustomerReturnOrderStatus;
using PaddyLotBagEntity = Backend.Domain.Entities.PaddyLotBag;
using PaddyLotBagAllocationEntity = Backend.Domain.Entities.PaddyLotBagAllocation;
using PartyDebtEntity = Backend.Domain.Entities.PartyDebt;
using DebtTransactionEntity = Backend.Domain.Entities.DebtTransaction;

namespace Backend.UnitTest.Services.PaddyLotTraceabilityTests;

[Trait("Service", "PaddyLotTraceability")]
public class PaddyLotTraceabilityServiceTests
{
    private readonly Mock<IHttpContextAccessor> _httpContextAccessorMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<PaddyLotTraceabilityService>> _loggerMock = new();

    public PaddyLotTraceabilityServiceTests()
    {
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);

        SetupUserClaims(orgId: 1, isSystemAdmin: true);
    }

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BackendContext(options);
    }

    private void SetupUserClaims(int orgId, bool isSystemAdmin = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimNames.OFFICE_ID, orgId.ToString()),
            new(ClaimNames.ID, "1")
        };
        if (isSystemAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, "ADMIN"));
            claims.Add(new Claim(ClaimNames.ROLE_IDS, "1001"));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        var claimsPrincipal = new ClaimsPrincipal(identity);

        var httpContextMock = new Mock<HttpContext>();
        httpContextMock.Setup(x => x.User).Returns(claimsPrincipal);

        _httpContextAccessorMock.Setup(x => x.HttpContext).Returns(httpContextMock.Object);
    }

    private PaddyLotTraceabilityService Sut(BackendContext context)
    {
        return new PaddyLotTraceabilityService(context, _httpContextAccessorMock.Object, _loggerFactoryMock.Object);
    }

    private async Task SeedBaseEntitiesAsync(BackendContext db)
    {
        var org = new OrganizationEntity { Id = 1, Code = "ORG1", Name = "Org 1" };
        var wh = new WarehouseEntity { Id = 1, Code = "WH01", Name = "Kho Tổng" };
        var statusInStock = new LotStatusEntity { Id = 1, Code = "IN_STOCK", Name = "Trong kho", Color = "#10B981", IsSellable = true };
        var statusQuarantine = new LotStatusEntity { Id = 2, Code = "QUARANTINE", Name = "Cách ly", Color = "#EF4444", IsSellable = false };
        var statusMilling = new LotStatusEntity { Id = 3, Code = "MILLING", Name = "Đang xay", Color = "#F59E0B", IsSellable = false };

        var cat = new ProductCategoryEntity { Id = 1, Name = "Lúa gạo", TreeIds = "1" };
        var uom = new UnitOfMeasureEntity { Id = 1, Name = "kg", Symbol = "kg" };
        var prod = new ProductEntity { Id = 1, ProductCategoryId = 1, Name = "Nông sản" };

        var pvPaddy = new ProductVariantEntity { Id = 1, ProductId = 1, UnitOfMeasureId = 1, SKU = "SKU-PADDY", Name = "Lúa tươi IR50404" };
        var pvRice = new ProductVariantEntity { Id = 2, ProductId = 1, UnitOfMeasureId = 1, SKU = "SKU-RICE", Name = "Gạo thành phẩm IR50404" };
        var pvBroken = new ProductVariantEntity { Id = 3, ProductId = 1, UnitOfMeasureId = 1, SKU = "SKU-BROKEN", Name = "Tấm IR50404" };

        var rv = new RiceVarietyEntity { Id = 1, Code = "IR50404", Name = "Giống lúa IR50404" };
        var farmer = new FarmerEntity { Id = 1, Code = "FARM01", Name = "Nguyễn Văn A", Phone = "0901234567" };
        var customer = new CustomerEntity { Id = 1, Code = "CUST01", Name = "Công ty Đại Nam", Phone = "0908888888" };
        var loc1 = new LocationEntity { Id = 1, WarehouseId = 1, ZoneName = "Zone A", SlotCode = "LOC-01", QrCode = "LC-01" };
        var loc2 = new LocationEntity { Id = 2, WarehouseId = 1, ZoneName = "Zone A", SlotCode = "LOC-02", QrCode = "LC-02" };
        var userInspector = new UserEntity { Id = 10, Username = "inspector1", Email = "inspector1@test.com", PasswordHash = "hash", FirstName = "Văn B", LastName = "Trần" };

        var obStatus = new OutboundOrderStatusEntity { Id = 1, Name = "Hoàn thành", Code = OutboundOrderStatusNames.Completed, Color = "#10B981" };
        var soStatus = new SalesOrderStatusEntity { Id = 1, Name = "Hoàn tất", Code = SalesOrderStatusNames.Completed, Color = "#10B981" };
        var moStatus = new MillingOrderStatusEntity { Id = 1, Name = "Hoàn thành", Code = "COMPLETED", Color = "#10B981" };

        db.Organizations.Add(org);
        db.Warehouses.Add(wh);
        db.LotStatuses.AddRange(statusInStock, statusQuarantine, statusMilling);
        db.ProductCategories.Add(cat);
        db.UnitOfMeasures.Add(uom);
        db.Products.Add(prod);
        db.ProductVariants.AddRange(pvPaddy, pvRice, pvBroken);
        db.RiceVarieties.Add(rv);
        db.Farmers.Add(farmer);
        db.Customers.Add(customer);
        db.Locations.AddRange(loc1, loc2);
        db.Users.Add(userInspector);
        db.OutboundOrderStatuses.Add(obStatus);
        db.SalesOrderStatuses.Add(soStatus);
        db.MillingOrderStatuses.Add(moStatus);

        await db.SaveChangesAsync();
    }

    // ── 1. Validation tests ──────────────────────────────────────────────────

    [Fact]
    public async Task GetByLotIdAsync_Returns400_WhenLotIdInvalid()
    {
        using var db = CreateContext();
        var service = Sut(db);

        var res = await service.GetByLotIdAsync(0);

        res.Status.Should().Be(400);
        res.Code.Should().Be("LOT_ID_INVALID");
    }

    [Fact]
    public async Task GetByLotCodeAsync_Returns400_WhenLotCodeEmpty()
    {
        using var db = CreateContext();
        var service = Sut(db);

        var res = await service.GetByLotCodeAsync("");

        res.Status.Should().Be(400);
        res.Code.Should().Be("LOT_CODE_REQUIRED");
    }

    [Fact]
    public async Task GetByLotIdAsync_Returns400_WhenMaxDepthInvalid()
    {
        using var db = CreateContext();
        var service = Sut(db);

        var res = await service.GetByLotIdAsync(1, maxDepth: 0);

        res.Status.Should().Be(400);
        res.Code.Should().Be("TRACEABILITY_MAX_DEPTH_INVALID");
    }

    [Fact]
    public async Task GetByLotIdAsync_Returns404_WhenLotNotFound()
    {
        using var db = CreateContext();
        var service = Sut(db);

        var res = await service.GetByLotIdAsync(999);

        res.Status.Should().Be(404);
        res.Code.Should().Be("PADDY_LOT_NOT_FOUND");
    }

    [Fact]
    public async Task GetByLotIdAsync_Returns404_WhenLotIsSoftDeleted()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        db.PaddyLots.Add(new PaddyLotEntity
        {
            Id = 1,
            LotCode = "LOT-DELETED",
            LotType = "PADDY",
            ProductVariantId = 1,
            StatusId = 1,
            WarehouseId = 1,
            IsDeleted = true
        });
        await db.SaveChangesAsync();

        var service = Sut(db);
        var res = await service.GetByLotIdAsync(1);

        res.Status.Should().Be(404);
        res.Code.Should().Be("PADDY_LOT_NOT_FOUND");
    }

    // ── 2. Multi-tenant / Org Access tests ───────────────────────────────────

    [Fact]
    public async Task GetByLotIdAsync_Returns403_WhenUserOrgIdMismatchAndNotAdmin()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        db.PaddyLots.Add(new PaddyLotEntity
        {
            Id = 10,
            LotCode = "LOT-ORG2",
            LotType = "PADDY",
            OrganizationId = 2, // Lot belongs to Org 2
            ProductVariantId = 1,
            StatusId = 1,
            WarehouseId = 1
        });
        await db.SaveChangesAsync();

        // User belongs to Org 1 and is NOT admin
        SetupUserClaims(orgId: 1, isSystemAdmin: false);
        var service = Sut(db);

        var res = await service.GetByLotIdAsync(10);

        res.Status.Should().Be(403);
        res.Code.Should().Be("ORGANIZATION_ACCESS_DENIED");
    }

    // ── 3. Traceability End-to-End Test (Procurement -> Quality -> Milling -> Outbound) ──

    [Fact]
    public async Task GetByLotIdAsync_TracesFullCycle_ProcurementToOutbound()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        // 1. Receipt
        var receipt = new PaddyPurchaseReceiptEntity
        {
            Id = 100,
            ReceiptCode = "REC-001",
            FarmerId = 1,
            RiceVarietyId = 1,
            WarehouseId = 1,
            ActualWeightKg = 5000m,
            ReceiptDate = DateTime.UtcNow.AddDays(-10),
            QualityJson = "{\"moisture\":14.5}"
        };
        db.PaddyPurchaseReceipts.Add(receipt);

        // 2. Input Paddy Lot
        var paddyLot = new PaddyLotEntity
        {
            Id = 101,
            LotCode = "LOT-PADDY-101",
            LotType = "PADDY",
            ProductVariantId = 1,
            RiceVarietyId = 1,
            StatusId = 1,
            WarehouseId = 1,
            SourceReceiptId = 100,
            InboundDate = DateTime.UtcNow.AddDays(-10),
            InitialWeightKg = 5000m,
            RemainingWeightKg = 0m,
            CostPricePerKg = 8000m
        };
        db.PaddyLots.Add(paddyLot);

        // 3. Quality Inspection for Input Lot
        var insp1 = new QualityInspectionEntity
        {
            Id = 201,
            PaddyLotId = 101,
            InspectedAt = DateTime.UtcNow.AddDays(-9),
            MoisturePercent = 14.5m,
            ImpurityPercent = 1.0m,
            PassedInspection = true,
            InspectorId = 10
        };
        db.QualityInspections.Add(insp1);

        // 4. Milling Order
        var milling = new MillingOrderEntity
        {
            Id = 300,
            MillingCode = "MO-001",
            StatusId = 1,
            WarehouseId = 1,
            YieldRateUsed = 0.70m,
            ComputedPaddyKg = 5000m,
            TotalRiceOutputKg = 3500m,
            ByproductKg = 1000m,
            LossKg = 500m,
            MachineRef = "MILL-01",
            OperatorId = 10,
            ActualPaddyInputKg = 5000m,
            ActualYieldRate = 0.70m,
            StartedAt = DateTime.UtcNow.AddDays(-8),
            CompletedAt = DateTime.UtcNow.AddDays(-7)
        };
        db.MillingOrders.Add(milling);

        var millingInput = new MillingOrderInputEntity
        {
            Id = 301,
            MillingOrderId = 300,
            PaddyLotId = 101,
            ConsumedWeightKg = 5000m
        };
        db.MillingOrderInputs.Add(millingInput);

        // Output Rice Lot
        var riceLot = new PaddyLotEntity
        {
            Id = 102,
            LotCode = "LOT-RICE-102",
            LotType = "RICE",
            ProductVariantId = 2,
            StatusId = 1,
            WarehouseId = 1,
            SourceMillingOrderId = 300,
            InboundDate = DateTime.UtcNow.AddDays(-7),
            InitialWeightKg = 3500m,
            RemainingWeightKg = 1500m
        };
        db.PaddyLots.Add(riceLot);

        var millingOutput = new MillingOrderOutputEntity
        {
            Id = 302,
            MillingOrderId = 300,
            ProductVariantId = 2,
            OutputLotId = 102,
            OutputType = "RICE",
            OutputWeightKg = 3500m,
            IsByproduct = false
        };
        db.MillingOrderOutputs.Add(millingOutput);

        // Output Byproduct Lot
        var byproductLot = new PaddyLotEntity
        {
            Id = 103,
            LotCode = "LOT-BYPRODUCT-103",
            LotType = "BYPRODUCT",
            ProductVariantId = 3,
            StatusId = 1,
            WarehouseId = 1,
            SourceMillingOrderId = 300,
            InboundDate = DateTime.UtcNow.AddDays(-7),
            InitialWeightKg = 1000m,
            RemainingWeightKg = 1000m
        };
        db.PaddyLots.Add(byproductLot);

        var millingOutputByproduct = new MillingOrderOutputEntity
        {
            Id = 303,
            MillingOrderId = 300,
            ProductVariantId = 3,
            OutputLotId = 103,
            OutputType = "BROKEN",
            OutputWeightKg = 1000m,
            IsByproduct = true
        };
        db.MillingOrderOutputs.Add(millingOutputByproduct);

        // 5. Sales Order and Outbound Order for Rice Lot
        var so = new SalesOrderEntity
        {
            Id = 400,
            SOCode = "SO-001",
            CustomerId = 1,
            StatusId = 1,
            Channel = "DIRECT",
            OrderDate = DateTime.UtcNow.AddDays(-5)
        };
        db.SalesOrders.Add(so);

        var ob = new OutboundOrderEntity
        {
            Id = 500,
            SalesOrderId = 400,
            WarehouseId = 1,
            OutboundOrderStatusId = 1,
            CompletedDate = DateTime.UtcNow.AddDays(-4)
        };
        db.OutboundOrders.Add(ob);

        var obItem = new OutboundOrderItemEntity
        {
            Id = 501,
            OutboundOrderId = 500,
            ProductVariantId = 2,
            QuantityOrdered = 2000m,
            QuantityPicked = 2000m
        };
        db.OutboundOrderItems.Add(obItem);

        var inv = new InventoryEntity { Id = 1, WarehouseId = 1, LocationId = 1, PaddyLotId = 102 };
        db.Inventories.Add(inv);

        var alloc = new OutboundOrderItemAllocationEntity
        {
            Id = 601,
            OutboundOrderItemId = 501,
            InventoryId = 1,
            PaddyLotId = 102,
            LocationId = 1,
            QuantityAllocated = 2000m,
            QuantityPicked = 2000m
        };
        db.OutboundOrderItemAllocations.Add(alloc);

        var deliveredBag = new PaddyLotBagEntity
        {
            Id = 650,
            LotId = 102,
            BagNo = 18,
            WeightKg = 50m,
            LocationId = 1,
            Status = "Dispatched",
            BagKind = "Finished"
        };
        db.PaddyLotBags.Add(deliveredBag);
        db.PaddyLotBagAllocations.Add(new PaddyLotBagAllocationEntity
        {
            Id = 651,
            BagId = 650,
            ReferenceType = "OUTBOUND_ORDER",
            ReferenceId = 500,
            ReferenceItemId = 501,
            AllocatedWeightKg = 50m,
            PickedWeightKg = 50m,
            BagWeightSnapshotKg = 50m,
            Status = "CONSUMED"
        });

        var feedback = new CustomerFeedbackEntity
        {
            Id = 700,
            SalesOrderId = 400,
            OutboundOrderId = 500,
            OutboundOrderItemId = 501,
            ProductVariantId = 2,
            PaddyLotBagAllocationId = 651,
            FeedbackType = "QUALITY",
            Severity = "HIGH",
            Description = "Bao gạo có mùi ẩm",
            ResolutionStatus = "RESOLVED",
            ResolutionNote = "Đã duyệt nhận lại hàng",
            CreatedDate = DateTime.UtcNow.AddDays(-3),
            ResolvedAt = DateTime.UtcNow.AddDays(-2)
        };
        db.CustomerFeedbacks.Add(feedback);

        db.CustomerReturnOrderStatuses.Add(new CustomerReturnStatusEntity
        {
            Id = 20,
            Code = "CONFIRMED",
            Name = "Đã hoàn tất",
            Color = "#10B981"
        });
        var firstRefundAt = DateTime.UtcNow.AddHours(-10);
        var secondRefundAt = DateTime.UtcNow.AddHours(-8);
        var debtReductionAt = DateTime.UtcNow.AddHours(-11);
        var returnLastModifiedAt = DateTime.UtcNow.AddHours(-6);
        var customerReturn = new CustomerReturnOrderEntity
        {
            Id = 800,
            OrganizationId = 1,
            WarehouseId = 1,
            CustomerReturnOrderStatusId = 20,
            OutboundOrderId = 500,
            CustomerFeedbackId = 700,
            CustomerId = 1,
            ReturnCode = "CR-001",
            ReturnReason = "Gạo có mùi ẩm",
            CreatedDate = DateTime.UtcNow.AddDays(-2),
            ReceivedAt = DateTime.UtcNow.AddDays(-1.5),
            InspectedAt = DateTime.UtcNow.AddDays(-1),
            ConfirmedAt = DateTime.UtcNow.AddHours(-12),
            ApprovedCreditAmount = 800000m,
            DebtReductionAmount = 300000m,
            RefundedAmount = 500000m,
            RefundStatus = "REFUNDED",
            LastModifiedDate = returnLastModifiedAt
        };
        db.CustomerReturnOrders.Add(customerReturn);
        db.CustomerReturnOrderItems.Add(new CustomerReturnOrderItemEntity
        {
            Id = 801,
            CustomerReturnOrderId = 800,
            ProductVariantId = 2,
            QuantityReturned = 50m,
            QuantityGood = 30m,
            QuantityDamaged = 15m,
            QualityStatus = "MIXED"
        });
        db.CustomerReturnOrderItemAllocations.Add(new CustomerReturnAllocationEntity
        {
            Id = 802,
            CustomerReturnOrderItemId = 801,
            OutboundOrderItemAllocationId = 601,
            PaddyLotId = 102,
            ProductVariantId = 2,
            OriginalLocationId = 1,
            QuantityReturned = 50m,
            QuantityReceived = 50m,
            QuantityGood = 30m,
            QuantityDamaged = 15m,
            QuantityRejected = 5m,
            RestockLocationId = 1,
            QuarantineLocationId = 2,
            Disposition = "MIXED",
            CreditAmount = 800000m
        });

        var refundDebt = new PartyDebtEntity
        {
            Id = 850,
            OrganizationId = 1,
            PartyType = LookupCodes.PartyType.Customer,
            PartyId = 1,
            Direction = LookupCodes.DebtDirection.Payable,
            CurrentBalance = 0,
            IsActive = true
        };
        db.PartyDebts.Add(refundDebt);
        db.DebtTransactions.AddRange(
            new DebtTransactionEntity
            {
                Id = 851,
                PartyDebtId = 850,
                TransactionType = LookupCodes.DebtTransactionType.Payment,
                Amount = 200000m,
                BalanceAfter = 300000m,
                RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                RefId = 800,
                TransactionDate = firstRefundAt,
                DeduplicationKey = "CRT-REFUND-800-BANK-001"
            },
            new DebtTransactionEntity
            {
                Id = 852,
                PartyDebtId = 850,
                TransactionType = LookupCodes.DebtTransactionType.Payment,
                Amount = 300000m,
                BalanceAfter = 0,
                RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                RefId = 800,
                TransactionDate = secondRefundAt,
                DeduplicationKey = "CRT-REFUND-800-BANK-002"
            },
            new DebtTransactionEntity
            {
                Id = 853,
                PartyDebtId = 850,
                TransactionType = LookupCodes.DebtTransactionType.ReturnCredit,
                Amount = 300000m,
                BalanceAfter = 0,
                RefType = InventoryReferenceTypeConstants.CustomerReturnOrder,
                RefId = 800,
                TransactionDate = debtReductionAt,
                DeduplicationKey = "CRT-CONFIRM-800"
            });

        await db.SaveChangesAsync();

        // Act: Trace from Paddy Lot 101
        var service = Sut(db);
        var res = await service.GetByLotIdAsync(101);

        // Assert
        res.Status.Should().Be(200);
        res.Resources.Should().NotBeNull();
        var data = (PaddyLotTraceabilityDto)res.Resources!;

        data.RequestedLotId.Should().Be(101);
        data.RequestedLotCode.Should().Be("LOT-PADDY-101");
        data.IsTruncated.Should().BeFalse();

        data.RelatedLots.Should().HaveCount(3);
        data.Purchases.Should().HaveCount(1);
        data.Purchases[0].ReceiptCode.Should().Be("REC-001");
        data.Purchases[0].InitialQuality.Should().NotBeNull();

        data.QualityInspections.Should().HaveCount(1);
        data.QualityInspections[0].PassedInspection.Should().BeTrue();
        data.QualityInspections[0].InspectorName.Should().Be("Trần Văn B");

        data.MillingOrders.Should().HaveCount(1);
        data.MillingOrders[0].MillingCode.Should().Be("MO-001");
        data.MillingOrders[0].Inputs.Should().HaveCount(1);
        data.MillingOrders[0].Outputs.Should().HaveCount(2);
        data.MillingOrders[0].MachineRef.Should().Be("MILL-01");
        data.MillingOrders[0].OperatorName.Should().Be("Trần Văn B");

        data.OutboundSales.Should().HaveCount(1);
        data.OutboundSales[0].SalesOrderCode.Should().Be("SO-001");
        data.OutboundSales[0].CustomerName.Should().Be("Công ty Đại Nam");

        data.CustomerFeedbacks.Should().ContainSingle();
        data.CustomerFeedbacks[0].BagNo.Should().Be(18);
        data.CustomerReturns.Should().ContainSingle();
        data.CustomerReturns[0].ReturnCode.Should().Be("CR-001");
        data.CustomerReturns[0].Items.Single().Allocations.Single().Disposition.Should().Be("MIXED");
        data.CustomerReturns[0].RefundedAmount.Should().Be(500000m);
        data.CustomerReturns[0].RefundedAt.Should().Be(secondRefundAt);
        data.CustomerReturns[0].RefundedAt.Should().NotBe(returnLastModifiedAt);

        data.Timeline.Should().NotBeEmpty();
        data.Timeline.Select(e => e.EventType).Should().Contain(new[]
        {
            "PROCUREMENT", "QUALITY_INSPECTION", "MILLING_STARTED", "MILLING_COMPLETED", "OUTBOUND_COMPLETED",
            "CUSTOMER_FEEDBACK_CREATED", "CUSTOMER_FEEDBACK_RESOLVED", "CUSTOMER_RETURN_CREATED",
            "CUSTOMER_RETURN_RECEIVED", "CUSTOMER_RETURN_INSPECTED", "CUSTOMER_RETURN_CONFIRMED", "CUSTOMER_RETURN_REFUND"
        });
        var refundEvents = data.Timeline.Where(e => e.EventType == "CUSTOMER_RETURN_REFUND").ToList();
        refundEvents.Should().HaveCount(2);
        refundEvents.Select(e => e.EventAt).Should().Equal(firstRefundAt, secondRefundAt);
        refundEvents.Select(e => e.Description).Should().Contain(x => x.Contains("200000"));
        refundEvents.Select(e => e.Description).Should().Contain(x => x.Contains("300000"));
        refundEvents.Should().NotContain(e => e.EventAt == debtReductionAt);

        // Summary calculations verification
        data.Summary.RelatedLotCount.Should().Be(3);
        data.Summary.PurchaseReceiptCount.Should().Be(1);
        data.Summary.PurchasedWeightKg.Should().Be(5000m);
        data.Summary.MillingInputWeightKg.Should().Be(5000m);
        data.Summary.MillingRiceOutputWeightKg.Should().Be(3500m);
        data.Summary.MillingByproductWeightKg.Should().Be(1000m);
        data.Summary.MillingLossWeightKg.Should().Be(500m);
        data.Summary.AllocatedOutboundWeightKg.Should().Be(2000m);
        data.Summary.DispatchedWeightKg.Should().Be(2000m);
        data.Summary.FeedbackCount.Should().Be(1);
        data.Summary.CustomerReturnCount.Should().Be(1);
        data.Summary.ReturnedWeightKg.Should().Be(50m);
        data.Summary.RestockedReturnWeightKg.Should().Be(30m);
        data.Summary.QuarantinedReturnWeightKg.Should().Be(15m);
        data.Summary.RejectedReturnWeightKg.Should().Be(5m);
        data.Summary.RefundAmount.Should().Be(500000m);
    }

    [Fact]
    public async Task GetByLotIdAsync_PurchaseBags_UseHistoricalSnapshotWeights()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var weighedAt = new DateTime(2026, 8, 20, 8, 30, 0, DateTimeKind.Utc);
        var receipt = new PaddyPurchaseReceiptEntity
        {
            Id = 100,
            ReceiptCode = "REC-BAGS",
            FarmerId = 1,
            WarehouseId = 1,
            ActualWeightKg = 171m,
            BagCount = 3,
            ReceiptDate = weighedAt,
            BagDetailsJson = System.Text.Json.JsonSerializer.Serialize(new[]
            {
                new Backend.Application.DTOs.InboundOrders.CreateBagDto { BagNo = 1, WeightKg = 56m, WeightCaptureMethod = "MANUAL", WeighedAt = weighedAt, WeighedBy = 10 },
                new Backend.Application.DTOs.InboundOrders.CreateBagDto { BagNo = 2, WeightKg = 57m, ScaleDeviceRef = "SCALE-01", WeightCaptureMethod = "SCALE", WeighedAt = weighedAt, WeighedBy = 10 },
                new Backend.Application.DTOs.InboundOrders.CreateBagDto { BagNo = 3, WeightKg = 58m, WeightCaptureMethod = "MANUAL", WeighedAt = weighedAt, WeighedBy = 10 }
            })
        };
        var paddyLot = new PaddyLotEntity
        {
            Id = 101,
            LotCode = "PADDY-BAGS",
            LotType = LotTypeConstants.Paddy,
            ProductVariantId = 1,
            StatusId = 1,
            WarehouseId = 1,
            SourceReceiptId = receipt.Id
        };
        db.PaddyPurchaseReceipts.Add(receipt);
        db.PaddyLots.Add(paddyLot);
        db.PaddyLotBags.AddRange(
            new PaddyLotBagEntity { Id = 11, LotId = 101, BagNo = 1, WeightKg = 56m, BagKind = "Purchase", Status = "Available" },
            new PaddyLotBagEntity { Id = 12, LotId = 101, BagNo = 2, WeightKg = 20m, BagKind = "Purchase", Status = "Available" },
            new PaddyLotBagEntity { Id = 13, LotId = 101, BagNo = 3, WeightKg = 0m, BagKind = "Purchase", Status = "Consumed" });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(paddyLot.Id);

        res.Status.Should().Be(200);
        var bags = ((PaddyLotTraceabilityDto)res.Resources!).Purchases.Single().Bags;
        bags.Should().HaveCount(3);
        bags.Select(x => x.BagId).Should().Equal(11, 12, 13);
        bags.Select(x => x.BagNo).Should().Equal(1, 2, 3);
        bags.Select(x => x.WeightKg).Should().Equal(56m, 57m, 58m);
        bags[1].ScaleDeviceRef.Should().Be("SCALE-01");
        bags[1].WeightCaptureMethod.Should().Be("SCALE");
        bags[1].WeighedAt.Should().Be(weighedAt);
        bags[1].WeighedBy.Should().Be(10);
        bags[1].WeighedByName.Should().Be("Trần Văn B");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{invalid json")]
    public async Task GetByLotIdAsync_PurchaseBags_FallBackToCurrentData_WhenSnapshotUnavailable(
        string? bagDetailsJson)
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var receipt = new PaddyPurchaseReceiptEntity
        {
            Id = 100,
            ReceiptCode = "REC-LEGACY",
            FarmerId = 1,
            WarehouseId = 1,
            ActualWeightKg = 12.5m,
            BagCount = 1,
            ReceiptDate = DateTime.UtcNow,
            BagDetailsJson = bagDetailsJson
        };
        var paddyLot = new PaddyLotEntity
        {
            Id = 101,
            LotCode = "PADDY-LEGACY",
            LotType = LotTypeConstants.Paddy,
            ProductVariantId = 1,
            StatusId = 1,
            WarehouseId = 1,
            SourceReceiptId = receipt.Id
        };
        db.PaddyPurchaseReceipts.Add(receipt);
        db.PaddyLots.Add(paddyLot);
        db.PaddyLotBags.Add(new PaddyLotBagEntity
        {
            Id = 11,
            LotId = paddyLot.Id,
            BagNo = 1,
            WeightKg = 12.5m,
            BagKind = "Purchase",
            Status = "Available"
        });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(paddyLot.Id);

        res.Status.Should().Be(200);
        var bag = ((PaddyLotTraceabilityDto)res.Resources!).Purchases.Single().Bags.Single();
        bag.BagId.Should().Be(11);
        bag.BagNo.Should().Be(1);
        bag.WeightKg.Should().Be(12.5m);
    }

    [Fact]
    public async Task GetByLotIdAsync_TracesBackward_FromRiceLotToPurchaseReceipt()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        // Paddy Lot -> Milling -> Rice Lot
        var paddyLot = new PaddyLotEntity { Id = 1, LotCode = "PADDY-1", LotType = "PADDY", ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        var riceLot = new PaddyLotEntity { Id = 2, LotCode = "RICE-2", LotType = "RICE", ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 10 };
        db.PaddyLots.AddRange(paddyLot, riceLot);

        var milling = new MillingOrderEntity { Id = 10, MillingCode = "MO-10", StatusId = 1, WarehouseId = 1, YieldRateUsed = 0.7m, TotalRiceOutputKg = 700m };
        db.MillingOrders.Add(milling);

        var input = new MillingOrderInputEntity { Id = 11, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 1000m };
        var output = new MillingOrderOutputEntity { Id = 12, MillingOrderId = 10, OutputLotId = 2, ProductVariantId = 2, OutputType = "RICE", OutputWeightKg = 700m };
        db.MillingOrderInputs.Add(input);
        db.MillingOrderOutputs.Add(output);

        await db.SaveChangesAsync();

        var service = Sut(db);
        var res = await service.GetByLotIdAsync(2); // Trace backward from Rice Lot 2

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;

        data.RequestedLotId.Should().Be(2);
        data.RelatedLots.Select(l => l.Id).Should().Contain(new[] { 1, 2 });
        data.MillingOrders.Should().HaveCount(1);
        data.MillingOrders[0].MillingCode.Should().Be("MO-10");
    }

    [Fact]
    public async Task GetByLotIdAsync_RiceLot_UsesOnlyItsDirectSourceMillingOrder()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var paddyLot = new PaddyLotEntity { Id = 1, LotCode = "PADDY-A", LotType = LotTypeConstants.Paddy, ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        var riceA = new PaddyLotEntity { Id = 2, LotCode = "RICE-A", LotType = LotTypeConstants.Rice, ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 10 };
        var riceB = new PaddyLotEntity { Id = 3, LotCode = "RICE-B", LotType = LotTypeConstants.Rice, ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 20 };
        var riceC = new PaddyLotEntity { Id = 4, LotCode = "RICE-C", LotType = LotTypeConstants.Rice, ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 30 };
        db.PaddyLots.AddRange(paddyLot, riceA, riceB, riceC);

        db.MillingOrders.AddRange(
            new MillingOrderEntity { Id = 10, MillingCode = "MO-01", StatusId = 1, WarehouseId = 1 },
            new MillingOrderEntity { Id = 20, MillingCode = "MO-02", StatusId = 1, WarehouseId = 1 },
            new MillingOrderEntity { Id = 30, MillingCode = "MO-03", StatusId = 1, WarehouseId = 1 });
        db.MillingOrderInputs.AddRange(
            new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m },
            new MillingOrderInputEntity { Id = 201, MillingOrderId = 20, PaddyLotId = 1, ConsumedWeightKg = 100m },
            new MillingOrderInputEntity { Id = 301, MillingOrderId = 30, PaddyLotId = 1, ConsumedWeightKg = 100m });
        db.MillingOrderOutputs.AddRange(
            new MillingOrderOutputEntity { Id = 102, MillingOrderId = 10, OutputLotId = 2, ProductVariantId = 2, OutputType = LotTypeConstants.Rice, OutputWeightKg = 70m },
            new MillingOrderOutputEntity { Id = 202, MillingOrderId = 20, OutputLotId = 3, ProductVariantId = 2, OutputType = LotTypeConstants.Rice, OutputWeightKg = 70m },
            new MillingOrderOutputEntity { Id = 302, MillingOrderId = 30, OutputLotId = 4, ProductVariantId = 2, OutputType = LotTypeConstants.Rice, OutputWeightKg = 70m });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(riceA.Id);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.MillingOrders.Select(x => x.MillingOrderId).Should().Equal(10);
        data.Summary.MillingOrderCount.Should().Be(1);
        data.RelatedLots.Select(x => x.Id).Should().BeEquivalentTo(new[] { paddyLot.Id, riceA.Id });
        data.Timeline
            .Where(x => x.ReferenceType == "MILLING_ORDER")
            .Select(x => x.ReferenceId)
            .Should().OnlyContain(x => x == 10);
    }

    [Fact]
    public async Task GetByLotIdAsync_PaddyLot_StillIncludesEveryMillingOrderThatConsumedIt()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var paddyLot = new PaddyLotEntity { Id = 1, LotCode = "PADDY-A", LotType = LotTypeConstants.Paddy, ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        db.PaddyLots.Add(paddyLot);
        db.MillingOrders.AddRange(
            new MillingOrderEntity { Id = 10, MillingCode = "MO-01", StatusId = 1, WarehouseId = 1 },
            new MillingOrderEntity { Id = 20, MillingCode = "MO-02", StatusId = 1, WarehouseId = 1 },
            new MillingOrderEntity { Id = 30, MillingCode = "MO-03", StatusId = 1, WarehouseId = 1 });
        db.MillingOrderInputs.AddRange(
            new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m },
            new MillingOrderInputEntity { Id = 201, MillingOrderId = 20, PaddyLotId = 1, ConsumedWeightKg = 100m },
            new MillingOrderInputEntity { Id = 301, MillingOrderId = 30, PaddyLotId = 1, ConsumedWeightKg = 100m });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(paddyLot.Id);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.MillingOrders.Select(x => x.MillingOrderId).Should().BeEquivalentTo(new[] { 10, 20, 30 });
        data.Summary.MillingOrderCount.Should().Be(3);
    }

    [Fact]
    public async Task GetByLotIdAsync_ByproductLot_UsesOnlyItsDirectSourceMillingOrder()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var paddyLot = new PaddyLotEntity { Id = 1, LotCode = "PADDY-A", LotType = LotTypeConstants.Paddy, ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        var branLot = new PaddyLotEntity { Id = 2, LotCode = "BRAN-A", LotType = LotTypeConstants.ByProduct, ProductVariantId = 3, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 10 };
        db.PaddyLots.AddRange(paddyLot, branLot);
        db.MillingOrders.AddRange(
            new MillingOrderEntity { Id = 10, MillingCode = "MO-01", StatusId = 1, WarehouseId = 1 },
            new MillingOrderEntity { Id = 20, MillingCode = "MO-02", StatusId = 1, WarehouseId = 1 });
        db.MillingOrderInputs.AddRange(
            new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m },
            new MillingOrderInputEntity { Id = 201, MillingOrderId = 20, PaddyLotId = 1, ConsumedWeightKg = 100m });
        db.MillingOrderOutputs.Add(
            new MillingOrderOutputEntity { Id = 102, MillingOrderId = 10, OutputLotId = 2, ProductVariantId = 3, OutputType = "BRAN", OutputWeightKg = 20m, IsByproduct = true });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(branLot.Id);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.MillingOrders.Select(x => x.MillingOrderId).Should().Equal(10);
        data.Summary.MillingOrderCount.Should().Be(1);
    }

    [Fact]
    public async Task GetByLotIdAsync_RiceLotWithoutSource_DoesNotInferMillingOrder()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var legacyRiceLot = new PaddyLotEntity { Id = 1, LotCode = "RICE-LEGACY", LotType = LotTypeConstants.Rice, ProductVariantId = 2, StatusId = 1, WarehouseId = 1 };
        db.PaddyLots.Add(legacyRiceLot);
        db.MillingOrders.Add(new MillingOrderEntity { Id = 10, MillingCode = "MO-10", StatusId = 1, WarehouseId = 1 });
        db.MillingOrderInputs.Add(new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m });
        await db.SaveChangesAsync();

        var res = await Sut(db).GetByLotIdAsync(legacyRiceLot.Id);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.MillingOrders.Should().BeEmpty();
        data.Summary.MillingOrderCount.Should().Be(0);
        data.Timeline.Should().NotContain(x => x.ReferenceType == "MILLING_ORDER");
    }

    // ── 4. Graph Cycle & Max Depth Tests ─────────────────────────────────────

    [Fact]
    public async Task GetByLotIdAsync_HandlesGraphCycles_WithoutInfiniteLoop()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        // Cyclic relationship: Lot 1 -> Milling 1 -> Lot 2 -> Milling 2 -> Lot 1
        var lot1 = new PaddyLotEntity { Id = 1, LotCode = "LOT-1", LotType = "PADDY", ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        var lot2 = new PaddyLotEntity { Id = 2, LotCode = "LOT-2", LotType = "RICE", ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 10 };
        db.PaddyLots.AddRange(lot1, lot2);

        var mo1 = new MillingOrderEntity { Id = 10, MillingCode = "MO-10", StatusId = 1, WarehouseId = 1, YieldRateUsed = 0.7m };
        var mo2 = new MillingOrderEntity { Id = 20, MillingCode = "MO-20", StatusId = 1, WarehouseId = 1, YieldRateUsed = 0.7m };
        db.MillingOrders.AddRange(mo1, mo2);

        db.MillingOrderInputs.Add(new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m });
        db.MillingOrderOutputs.Add(new MillingOrderOutputEntity { Id = 102, MillingOrderId = 10, OutputLotId = 2, ProductVariantId = 2, OutputType = "RICE", OutputWeightKg = 70m });

        db.MillingOrderInputs.Add(new MillingOrderInputEntity { Id = 201, MillingOrderId = 20, PaddyLotId = 2, ConsumedWeightKg = 70m });
        db.MillingOrderOutputs.Add(new MillingOrderOutputEntity { Id = 202, MillingOrderId = 20, OutputLotId = 1, ProductVariantId = 1, OutputType = "RICE", OutputWeightKg = 50m });

        await db.SaveChangesAsync();

        var service = Sut(db);
        var res = await service.GetByLotIdAsync(1);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.IsTruncated.Should().BeFalse();
        data.RelatedLots.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByLotIdAsync_SetsIsTruncatedTrue_WhenMaxDepthExceeded()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        // Chain of lots: Lot 1 -> MO 1 -> Lot 2 -> MO 2 -> Lot 3
        var lot1 = new PaddyLotEntity { Id = 1, LotCode = "LOT-1", LotType = "PADDY", ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        var lot2 = new PaddyLotEntity { Id = 2, LotCode = "LOT-2", LotType = "RICE", ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 10 };
        var lot3 = new PaddyLotEntity { Id = 3, LotCode = "LOT-3", LotType = "RICE", ProductVariantId = 2, StatusId = 1, WarehouseId = 1, SourceMillingOrderId = 20 };
        db.PaddyLots.AddRange(lot1, lot2, lot3);

        db.MillingOrders.Add(new MillingOrderEntity { Id = 10, MillingCode = "MO-10", StatusId = 1, WarehouseId = 1 });
        db.MillingOrders.Add(new MillingOrderEntity { Id = 20, MillingCode = "MO-20", StatusId = 1, WarehouseId = 1 });

        db.MillingOrderInputs.Add(new MillingOrderInputEntity { Id = 101, MillingOrderId = 10, PaddyLotId = 1, ConsumedWeightKg = 100m });
        db.MillingOrderOutputs.Add(new MillingOrderOutputEntity { Id = 102, MillingOrderId = 10, OutputLotId = 2, ProductVariantId = 2, OutputType = "RICE", OutputWeightKg = 70m });

        db.MillingOrderInputs.Add(new MillingOrderInputEntity { Id = 201, MillingOrderId = 20, PaddyLotId = 2, ConsumedWeightKg = 70m });
        db.MillingOrderOutputs.Add(new MillingOrderOutputEntity { Id = 202, MillingOrderId = 20, OutputLotId = 3, ProductVariantId = 2, OutputType = "RICE", OutputWeightKg = 50m });

        await db.SaveChangesAsync();

        var service = Sut(db);
        // Call with maxDepth = 1
        var res = await service.GetByLotIdAsync(1, maxDepth: 1);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.IsTruncated.Should().BeTrue();
    }

    // ── 5. Soft Delete Exclusion Test ─────────────────────────────────────────

    [Fact]
    public async Task GetByLotIdAsync_IgnoresSoftDeletedChildEntities()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var paddyLot = new PaddyLotEntity { Id = 1, LotCode = "LOT-1", LotType = "PADDY", ProductVariantId = 1, StatusId = 1, WarehouseId = 1 };
        db.PaddyLots.Add(paddyLot);

        var activeInsp = new QualityInspectionEntity { Id = 1, PaddyLotId = 1, InspectedAt = DateTime.UtcNow, PassedInspection = true, IsDeleted = false };
        var deletedInsp = new QualityInspectionEntity { Id = 2, PaddyLotId = 1, InspectedAt = DateTime.UtcNow, PassedInspection = false, IsDeleted = true };
        db.QualityInspections.AddRange(activeInsp, deletedInsp);

        await db.SaveChangesAsync();

        var service = Sut(db);
        var res = await service.GetByLotIdAsync(1);

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.QualityInspections.Should().HaveCount(1);
        data.QualityInspections[0].InspectionId.Should().Be(1);
    }

    // ── 6. GetByLotCodeAsync Test ─────────────────────────────────────────────

    [Fact]
    public async Task GetByLotCodeAsync_ReturnsTraceability_WhenValidLotCode()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        db.PaddyLots.Add(new PaddyLotEntity { Id = 10, LotCode = "LOT-CODE-ABC", LotType = "PADDY", ProductVariantId = 1, StatusId = 1, WarehouseId = 1 });
        await db.SaveChangesAsync();

        var service = Sut(db);
        var res = await service.GetByLotCodeAsync("LOT-CODE-ABC");

        res.Status.Should().Be(200);
        var data = (PaddyLotTraceabilityDto)res.Resources!;
        data.RequestedLotId.Should().Be(10);
        data.RequestedLotCode.Should().Be("LOT-CODE-ABC");
    }

    [Fact]
    public async Task GetByLotIdAsync_ReturnsMixedPhysicalBag_FromEveryContentLot()
    {
        using var db = CreateContext();
        await SeedBaseEntitiesAsync(db);

        var ownerLot = new PaddyLotEntity
        {
            Id = 201,
            LotCode = "LOT-A",
            LotType = LotTypeConstants.Rice,
            ProductVariantId = 2,
            StatusId = 1,
            WarehouseId = 1
        };
        var contentLot = new PaddyLotEntity
        {
            Id = 202,
            LotCode = "LOT-B",
            LotType = LotTypeConstants.Rice,
            ProductVariantId = 2,
            StatusId = 1,
            WarehouseId = 1
        };
        var bag = new PaddyLotBagEntity
        {
            Id = 301,
            LotId = ownerLot.Id,
            BagNo = 15,
            WeightKg = 10m,
            StandardWeightKg = 10m,
            IsFull = true,
            BagKind = PaddyLotBagKinds.Finished,
            Status = PaddyLotBagStatuses.Stored,
            LocationId = 1
        };

        db.PaddyLots.AddRange(ownerLot, contentLot);
        db.PaddyLotBags.Add(bag);
        db.PaddyLotBagContents.AddRange(
            new Backend.Domain.Entities.PaddyLotBagContent
            {
                BagId = bag.Id,
                LotId = ownerLot.Id,
                WeightKg = 6m
            },
            new Backend.Domain.Entities.PaddyLotBagContent
            {
                BagId = bag.Id,
                LotId = contentLot.Id,
                WeightKg = 1m
            },
            new Backend.Domain.Entities.PaddyLotBagContent
            {
                BagId = bag.Id,
                LotId = contentLot.Id,
                WeightKg = 3m
            });
        await db.SaveChangesAsync();

        var ownerResponse = await Sut(db).GetByLotIdAsync(ownerLot.Id);
        var contentResponse = await Sut(db).GetByLotIdAsync(contentLot.Id);

        ownerResponse.Status.Should().Be(200);
        contentResponse.Status.Should().Be(200);
        foreach (var response in new[] { ownerResponse, contentResponse })
        {
            var physicalBag = ((PaddyLotTraceabilityDto)response.Resources!).PhysicalBags.Single();
            physicalBag.BagId.Should().Be(bag.Id);
            physicalBag.OwnerLotId.Should().Be(ownerLot.Id);
            physicalBag.IsMixedLot.Should().BeTrue();
            physicalBag.ContentLotCount.Should().Be(2);
            physicalBag.Contents.Should().ContainSingle(x =>
                x.LotId == ownerLot.Id && x.WeightKg == 6m && x.Percentage == 60m);
            physicalBag.Contents.Should().ContainSingle(x =>
                x.LotId == contentLot.Id && x.WeightKg == 4m && x.Percentage == 40m);
        }
    }
}
