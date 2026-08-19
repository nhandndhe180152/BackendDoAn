using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakes;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using MockQueryable.Moq;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.StockTakes;

/// <summary>
/// Kiểm kê theo BAO — các quy tắc dễ sai nhất khi tính lại số liệu.
/// </summary>
[Trait("Service", "StockTake")]
public class StockTakeBagLogicTests
{
    private readonly Mock<IStockTakeRepository> _stockTakeRepo = new();
    private readonly Mock<IStockTakeItemRepository> _stockTakeItemRepo = new();
    private readonly Mock<IInventoryTransactionService> _invTxService = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<ISystemConfigRepository> _sysConfigRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IApplicationDbContext> _dbContext = new();

    private StockTakeService Sut() => new(
        _stockTakeRepo.Object, _stockTakeItemRepo.Object, _invTxService.Object,
        _invRepo.Object, _sysConfigRepo.Object, _http.Object, _dispatcher.Object,
        _dbContext.Object);

    private static StockTakeItemBag Bag(
        int id,
        decimal systemKg,
        bool counted = false,
        decimal? countedKg = null,
        string disposition = StockTakeBagDispositions.Keep) =>
        new()
        {
            Id = id,
            PaddyLotBagId = id * 10,
            BagNo = id,
            SystemWeightKg = systemKg,
            PickSequence = id,
            RestowSequence = id,
            Counted = counted,
            CountedWeightKg = countedKg,
            Disposition = disposition
        };

    // ─── Khối lượng của một bao ──────────────────────────────────────────────

    [Fact]
    public void Bag_NotFound_ContributesNothing()
    {
        // Bao không tìm thấy = đã mất khỏi kho, không được tính kg.
        Bag(1, 50m, counted: false).EffectiveWeightKg.Should().Be(0m);
    }

    [Fact]
    public void Bag_FoundButNotWeighed_KeepsBookWeight()
    {
        // Thủ kho chỉ cân bao nghi ngờ. Bao không cân phải GIỮ NGUYÊN kg sổ sách,
        // không quy về 0 và cũng không bị chia đều chênh lệch một cách bịa đặt.
        Bag(1, 50m, counted: true).EffectiveWeightKg.Should().Be(50m);
    }

    [Fact]
    public void Bag_FoundAndWeighed_UsesScaleReading()
    {
        Bag(1, 50m, counted: true, countedKg: 48.4m).EffectiveWeightKg.Should().Be(48.4m);
    }

    [Fact]
    public void Bag_DisposedIsStillFound_ButLeavesTheLocation()
    {
        // Bao hỏng vẫn là bao TÌM THẤY (không phải mất trộm) nên vẫn tính vào kg
        // đếm được; nó chỉ rời khỏi vị trí ở bước xử lý.
        var bag = Bag(1, 50m, counted: true, disposition: StockTakeBagDispositions.Dispose);
        bag.EffectiveWeightKg.Should().Be(50m);
        bag.StaysAtLocation.Should().BeFalse();
        bag.LeavesLocation.Should().BeTrue();
    }

    [Theory]
    [InlineData(StockTakeBagDispositions.Keep, true)]
    [InlineData(StockTakeBagDispositions.Quarantine, false)]
    [InlineData(StockTakeBagDispositions.Release, false)]
    [InlineData(StockTakeBagDispositions.Dispose, false)]
    public void Bag_StaysAtLocation_OnlyWhenKept(string disposition, bool stays)
    {
        Bag(1, 50m, counted: true, disposition: disposition)
            .StaysAtLocation.Should().Be(stays);
    }

    [Theory]
    [InlineData("KEEP", true)]
    [InlineData("QUARANTINE", true)]
    [InlineData("DISPOSE", true)]
    [InlineData("RELEASE", true)]
    [InlineData("keep", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("MOVE_SOMEWHERE", false)]
    public void Disposition_IsValid_OnlyAcceptsKnownUppercaseCodes(string? value, bool expected)
    {
        StockTakeBagDispositions.IsValid(value).Should().Be(expected);
    }

    // ─── Chênh lệch số bao của một dòng ─────────────────────────────────────

    [Fact]
    public void Item_BagDifference_IsNegativeWhenBagsAreMissing()
    {
        var item = new StockTakeItem { SystemBagCount = 10, CountedBagCount = 9 };
        item.BagDifference.Should().Be(-1);
        item.HasBagVariance.Should().BeTrue();
    }

    [Fact]
    public void Item_WithoutCount_HasNoBagVarianceYet()
    {
        // Dòng chưa kiểm thì chưa thể coi là thiếu bao.
        var item = new StockTakeItem { SystemBagCount = 10, CountedBagCount = null };
        item.HasBagVariance.Should().BeFalse();
    }

    [Fact]
    public void Item_MovedBagsDoNotCreateBagVariance()
    {
        // Chuyển cách ly / bỏ bao hỏng là QUYẾT ĐỊNH XỬ LÝ, không phải hụt kho:
        // cả 3 bao đều tìm thấy nên số bao không lệch.
        var item = new StockTakeItem
        {
            SystemBagCount = 3,
            CountedBagCount = 3,
            Bags = new List<StockTakeItemBag>
            {
                Bag(1, 50m, counted: true),
                Bag(2, 50m, counted: true, disposition: StockTakeBagDispositions.Quarantine),
                Bag(3, 50m, counted: true, disposition: StockTakeBagDispositions.Dispose),
            }
        };

        item.HasBagVariance.Should().BeFalse();
        item.Bags.Sum(x => x.EffectiveWeightKg).Should().Be(150m);
        item.Bags.Where(x => x.StaysAtLocation).Sum(x => x.EffectiveWeightKg).Should().Be(50m);
    }

    // ─── Mức chênh lệch hiển thị ────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_MissingOneBag_IsLarge_EvenWhenKilogramGapIsTiny()
    {
        // Một bao 50 kg trong cột 5 tấn chỉ lệch 1% → theo ngưỡng kg/% sẽ là SMALL
        // và trôi qua khâu duyệt. Nhưng mất nguyên một bao là sự cố an ninh kho.
        SetupThresholds();
        var item = new StockTakeItem
        {
            Id = 1,
            StockTakeId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            SystemQuantity = 5000m,
            ActualQuantity = 4950m,
            SystemBagCount = 100,
            CountedBagCount = 99,
            Bags = new List<StockTakeItemBag> { Bag(1, 50m, counted: false) }
        };
        SetupFind(new StockTake
        {
            Id = 1,
            WarehouseId = 1,
            STCode = "ST-001",
            StockTakeStatusId = 1,
            StockTakeItems = new List<StockTakeItem> { item }
        });

        var response = await Sut().GetByIdAsync(1);

        response.Status.Should().Be(200);
        var dto = (response.Resources as StockTakeDto)!;
        dto.StockTakeItems.Single().BagDifference.Should().Be(-1);
        dto.StockTakeItems.Single().VarianceSeverity.Should().Be("LARGE",
            "mất nguyên một bao phải được nâng mức, không tính theo % kg");
    }

    [Fact]
    public async Task GetByIdAsync_NoBagGap_StillUsesKilogramThresholds()
    {
        SetupThresholds();
        var item = new StockTakeItem
        {
            Id = 1,
            StockTakeId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            SystemQuantity = 5000m,
            ActualQuantity = 4999m,   // 1 kg = 0,02% → SMALL
            SystemBagCount = 100,
            CountedBagCount = 100,
        };
        SetupFind(new StockTake
        {
            Id = 1,
            WarehouseId = 1,
            STCode = "ST-002",
            StockTakeStatusId = 1,
            StockTakeItems = new List<StockTakeItem> { item }
        });

        var response = await Sut().GetByIdAsync(1);

        response.Status.Should().Be(200);
        var dto = (response.Resources as StockTakeDto)!;
        dto.StockTakeItems.Single().VarianceSeverity.Should().Be("SMALL");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private void SetupThresholds()
    {
        _sysConfigRepo.Setup(r => r.GetValueByKey(It.IsAny<string>())).ReturnsAsync((string?)null);
    }

    private void SetupFind(params StockTake[] items)
    {
        _stockTakeRepo
            .Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<StockTake, bool>>>(),
                It.IsAny<bool>()))
            .Returns(items.ToList().AsQueryable().BuildMock());
    }
}
