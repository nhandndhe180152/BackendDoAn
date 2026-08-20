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

    // ─── Quét tem QR cột ────────────────────────────────────────────────────

    [Fact]
    public async Task ResolveScopeQr_ReadsTheStockliteLabelPayload()
    {
        // Tem in ra không chứa mỗi mã cột mà là cả payload có tiền tố; so khớp
        // nguyên chuỗi với Location.QrCode thì không bao giờ ra.
        SetupLocations(Location(7, "LC-ABC", zone: "Khu A", slot: "A-01"));
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("STOCKLITE|1|LOCATION|LC-ABC", 1);

        var dto = (response.Resources as StockTakeScopeResolveDto)!;
        dto.Matched.Should().BeTrue();
        dto.LocationId.Should().Be(7);
        dto.ScopeType.Should().Be("COLUMN");
    }

    [Fact]
    public async Task ResolveScopeQr_AcceptsAPlainCodeTypedByHand()
    {
        SetupLocations(Location(7, "LC-ABC"));
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("  LC-ABC  ", null);

        (response.Resources as StockTakeScopeResolveDto)!.Matched.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveScopeQr_RecoversWhenThePayloadArrivesStillPercentEncoded()
    {
        // Ký tự '|' đi qua query string có thể bị encode hai lần; chuỗi tới nơi
        // vẫn còn %7C thì phải giải mã trước khi tách.
        SetupLocations(Location(7, "LC-ABC"));
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("STOCKLITE%7C1%7CLOCATION%7CLC-ABC", 1);

        (response.Resources as StockTakeScopeResolveDto)!.Matched.Should().BeTrue();
    }

    [Fact]
    public async Task ResolveScopeQr_MatchesEvenWhenTheAppHasAnotherWarehouseOpen()
    {
        // Quét đúng tem mà báo "không khớp" chỉ vì app đang mở kho khác là sai:
        // trả về cột kèm kho của nó để màn hình tự chuyển theo.
        SetupLocations(Location(7, "LC-ABC", warehouseId: 9));
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("STOCKLITE|9|LOCATION|LC-ABC", warehouseId: 1);

        var dto = (response.Resources as StockTakeScopeResolveDto)!;
        dto.Matched.Should().BeTrue();
        dto.WarehouseId.Should().Be(9);
        dto.Message.Should().Contain("kho khác");
    }

    [Fact]
    public async Task ResolveScopeQr_RejectsABagLabel()
    {
        SetupLocations(Location(7, "LC-ABC"));
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("STOCKLITE|1|BAG|PLB-123", 1);

        var dto = (response.Resources as StockTakeScopeResolveDto)!;
        dto.Matched.Should().BeFalse();
        dto.Message.Should().Contain("BAO");
    }

    [Fact]
    public async Task ResolveScopeQr_RefusesTheOutboundStagingArea()
    {
        var staging = Location(8, "LC-STG");
        staging.IsOutboundStaging = true;
        SetupLocations(staging);
        SetupEmptyStock();

        var response = await Sut().ResolveScopeQrAsync("STOCKLITE|1|LOCATION|LC-STG", 1);

        (response.Resources as StockTakeScopeResolveDto)!.Matched.Should().BeFalse();
    }

    // ─── Danh sách cột để chọn phạm vi ──────────────────────────────────────

    [Fact]
    public async Task GetScopeOptions_ListsColumnsThatOnlyHaveInventoryRows()
    {
        // Dữ liệu nhập kho từ trước khi quản lý theo bao chỉ có dòng Inventory.
        // Liệt kê theo bao thôi thì gần hết kho biến mất khỏi danh sách chọn.
        var withBags = Location(7, "LC-A", slot: "A-01");
        var inventoryOnly = Location(8, "LC-B", slot: "A-02");
        SetupLocations(withBags, inventoryOnly);
        SetupInventories(
            Inventory(withBags.Id, 500m),
            Inventory(inventoryOnly.Id, 320m));
        SetupBags(StoredBag(1, withBags, 50m), StoredBag(2, withBags, 50m));

        var response = await Sut().GetScopeOptionsAsync(1, null);

        var dto = (response.Resources as StockTakeScopeOptionsDto)!;
        dto.Columns.Should().HaveCount(2);
        dto.Columns.Single(x => x.LocationId == inventoryOnly.Id).BagCount.Should().Be(0);
        dto.Columns.Single(x => x.LocationId == inventoryOnly.Id).TotalWeightKg.Should().Be(320m);
        dto.Columns.Single(x => x.LocationId == withBags.Id).BagCount.Should().Be(2);
    }

    [Fact]
    public async Task GetScopeOptions_SkipsEmptyColumnsAndTheOutboundStagingArea()
    {
        var stocked = Location(7, "LC-A");
        var empty = Location(8, "LC-B");
        var staging = Location(9, "LC-STG");
        staging.IsOutboundStaging = true;
        SetupLocations(stocked, empty, staging);
        SetupInventories(Inventory(stocked.Id, 100m), Inventory(staging.Id, 80m), Inventory(empty.Id, 0m));
        SetupBags();

        var response = await Sut().GetScopeOptionsAsync(1, null);

        var dto = (response.Resources as StockTakeScopeOptionsDto)!;
        dto.Columns.Select(x => x.LocationId).Should().Equal(stocked.Id);
    }

    [Fact]
    public async Task GetScopeOptions_QuarantineOnlyKeepsOnlyQuarantineColumns()
    {
        var normal = Location(7, "LC-A");
        var quarantine = Location(8, "LC-Q");
        quarantine.IsQuarantine = true;
        SetupLocations(normal, quarantine);
        SetupInventories(Inventory(normal.Id, 100m), Inventory(quarantine.Id, 60m));
        SetupBags();

        var response = await Sut().GetScopeOptionsAsync(1, quarantineOnly: true);

        var dto = (response.Resources as StockTakeScopeOptionsDto)!;
        dto.Columns.Select(x => x.LocationId).Should().Equal(quarantine.Id);
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    private static Backend.Domain.Entities.Location Location(
        int id, string qrCode, int warehouseId = 1, string zone = "Khu A", string? slot = null) =>
        new()
        {
            Id = id,
            WarehouseId = warehouseId,
            ZoneName = zone,
            SlotCode = slot ?? $"S-{id}",
            QrCode = qrCode,
            IsActive = true
        };

    private static Backend.Domain.Entities.Inventory Inventory(int locationId, decimal kg, int warehouseId = 1) =>
        new()
        {
            Id = locationId * 100,
            WarehouseId = warehouseId,
            LocationId = locationId,
            ProductVariantId = 1,
            QuantityOnHand = kg
        };

    private static PaddyLotBag StoredBag(int id, Backend.Domain.Entities.Location location, decimal kg) =>
        new()
        {
            Id = id,
            LotId = 1,
            BagNo = id,
            WeightKg = kg,
            LocationId = location.Id,
            Location = location,
            Status = "Stored"
        };

    private void SetupLocations(params Backend.Domain.Entities.Location[] locations) =>
        _dbContext.Setup(c => c.Locations)
            .Returns(locations.ToList().AsQueryable().BuildMockDbSet().Object);

    private void SetupInventories(params Backend.Domain.Entities.Inventory[] inventories) =>
        _dbContext.Setup(c => c.Inventories)
            .Returns(inventories.ToList().AsQueryable().BuildMockDbSet().Object);

    private void SetupBags(params PaddyLotBag[] bags) =>
        _dbContext.Setup(c => c.PaddyLotBags)
            .Returns(bags.ToList().AsQueryable().BuildMockDbSet().Object);

    /// <summary>Tồn kho và bao rỗng — dùng cho các test chỉ quan tâm việc tra tem.</summary>
    private void SetupEmptyStock()
    {
        SetupInventories();
        SetupBags();
    }


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
