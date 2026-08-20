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
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using MockQueryable.Moq;
using Xunit;

namespace Backend.UnitTest.Services.StockTakes;

[Trait("Service", "StockTake")]
public class StockTakeServiceTests
{
    private readonly Mock<IStockTakeRepository> _stockTakeRepo = new();
    private readonly Mock<IStockTakeItemRepository> _stockTakeItemRepo = new();
    private readonly Mock<IInventoryTransactionService> _invTxService = new();
    private readonly Mock<IInventoryRepository> _invRepo = new();
    private readonly Mock<ISystemConfigRepository> _sysConfigRepo = new();
    private readonly Mock<IHttpContextAccessor> _http = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();
    private readonly Mock<IApplicationDbContext> _dbContext = new();

    public StockTakeServiceTests()
    {
        // Kiểm kê theo BAO: CreateAsync đọc thêm Location (để biết phạm vi có phải
        // khu cách ly không) và PaddyLotBag (để chụp danh sách bao của cột).
        // Mock IApplicationDbContext trả null cho DbSet chưa setup → NullReference,
        // nên cho mặc định RỖNG ở đây; test nào cần dữ liệu thì Setup đè lên.
        _dbContext.Setup(c => c.Locations)
            .Returns(new List<Backend.Domain.Entities.Location>()
                .AsQueryable().BuildMockDbSet().Object);
        _dbContext.Setup(c => c.PaddyLotBags)
            .Returns(new List<Backend.Domain.Entities.PaddyLotBag>()
                .AsQueryable().BuildMockDbSet().Object);
        _dbContext.Setup(c => c.Inventories)
            .Returns(new List<Backend.Domain.Entities.Inventory>()
                .AsQueryable().BuildMockDbSet().Object);
    }

    private StockTakeService Sut() => new(
        _stockTakeRepo.Object, _stockTakeItemRepo.Object, _invTxService.Object,
        _invRepo.Object, _sysConfigRepo.Object, _http.Object, _dispatcher.Object,
        _dbContext.Object);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Setup SystemConfig mock trả về ngưỡng SMALL / MEDIUM từ DB (cả % lẫn kg).</summary>
    private void SetupVarianceThresholds(
        decimal smallPct  = 0.5m,
        decimal mediumPct = 2.0m,
        decimal smallKg   = 5.0m,
        decimal mediumKg  = 20.0m)
    {
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVariancePercent))
            .ReturnsAsync(smallPct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVariancePercent))
            .ReturnsAsync(mediumPct.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVarianceKg))
            .ReturnsAsync(smallKg.ToString(System.Globalization.CultureInfo.InvariantCulture));
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVarianceKg))
            .ReturnsAsync(mediumKg.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>Setup SystemConfig mock không có bản ghi → service dùng Defaults constants.</summary>
    private void SetupVarianceThresholdsNotConfigured()
    {
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(It.IsAny<string>()))
            .ReturnsAsync((string?)null);
    }

    private static StockTake MakeStockTake(int id, params StockTakeItem[] items) =>
        new()
        {
            Id = id,
            WarehouseId = 1,
            STCode = $"ST-{id:000}",
            StockTakeStatusId = 1,
            IsDeleted = false,
            StockTakeItems = items.ToList()
        };

    private static StockTakeItem MakeItem(decimal system, decimal? actual, int? paddyLotId = null) =>
        new()
        {
            Id = 1,
            StockTakeId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            PaddyLotId = paddyLotId,
            SystemQuantity = system,
            ActualQuantity = actual,
            Note = null
        };

    private void SetupStockTakeFind(params StockTake[] items)
    {
        _stockTakeRepo
            .Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<StockTake, bool>>>(),
                It.IsAny<bool>()))
            .Returns(items.ToList().AsQueryable().BuildMock());
    }

    // ─── GetByIdAsync — không tìm thấy ───────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        SetupStockTakeFind(); // empty

        var result = await Sut().GetByIdAsync(999);

        result.Status.Should().Be(404);
    }

    // ─── ClassifySeverity qua GetByIdAsync (ngưỡng FDS: 0.5% / 2% / 5kg / 20kg) ─────

    [Theory]
    [InlineData(100, 100,   "NONE")]   // Không chênh lệch → NONE
    [InlineData(100, 99.6,  "SMALL")]  // 0.4% ≤ 0.5% VÀ 0.4kg ≤ 5kg → SMALL
    [InlineData(100, 98.5,  "MEDIUM")] // 1.5% ≤ 2% → MEDIUM (theo %)
    [InlineData(100, 90,    "LARGE")]  // 10% > 2% → LARGE (theo %)
    [InlineData(100, 110,   "LARGE")]  // Thừa 10% → LARGE
    public async Task GetByIdAsync_VarianceSeverity_ClassifiesCorrectly_ByPercent(
        decimal system, decimal actual, string expectedSeverity)
    {
        // Arrange — dùng ngưỡng FDS chuẩn
        SetupVarianceThresholds(smallPct: 0.5m, mediumPct: 2.0m, smallKg: 5m, mediumKg: 20m);
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system, actual)));

        // Act
        var result = await Sut().GetByIdAsync(1);

        // Assert
        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be(expectedSeverity);
    }

    /// <summary>
    /// FDS: chênh lệch tuyệt đối 30 kg trên lô 10.000 kg chỉ là 0.3%
    /// nhưng 30 kg > 20 kg → mức tính theo kg là LARGE → kết quả cuối là LARGE.
    /// Đây là case quan trọng nhất mà code cũ bị sai.
    /// </summary>
    [Fact]
    public async Task GetByIdAsync_SmallPercentButLargeKg_ReturnsLarge()
    {
        // 30 kg / 10000 kg = 0.3% → theo % là SMALL, nhưng 30 kg > 20 kg → theo kg là LARGE
        SetupVarianceThresholds(smallPct: 0.5m, mediumPct: 2.0m, smallKg: 5m, mediumKg: 20m);
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 10000m, actual: 9970m)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("LARGE",
            "chênh 30 kg vượt ngưỡng MEDIUM 20 kg dù % chỉ 0.3%");
    }

    [Fact]
    public async Task GetByIdAsync_SystemQuantityZero_ActualQuantityPositive_VarianceSeverityIsLarge()
    {
        // SystemQuantity = 0 và ActualQuantity = 10 → chênh lệch nghiêm trọng → LARGE
        SetupVarianceThresholds();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 0, actual: 10)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("LARGE");
    }

    [Fact]
    public async Task GetByIdAsync_ActualNull_VarianceSeverityIsNone()
    {
        // ActualQuantity chưa nhập → VariancePercent = null → ClassifySeverity → NONE
        SetupVarianceThresholds();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: null)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("NONE");
    }

    // ─── Fallback về Defaults khi DB chưa có config ───────────────────────────

    [Fact]
    public async Task GetByIdAsync_ConfigNotInDb_FallsBackToDefaults_ClassifiesMedium()
    {
        // 1% chênh lệch, 1 kg chênh → với default FDS (SMALL=0.5%, MEDIUM=2%, SmallKg=5, MediumKg=20) → MEDIUM (theo %)
        SetupVarianceThresholdsNotConfigured();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: 99)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("MEDIUM");
    }

    [Fact]
    public async Task GetByIdAsync_ConfigNotInDb_FallsBackToDefaults_ClassifiesLarge()
    {
        // 10% chênh lệch → với default FDS (SMALL=0.5%, MEDIUM=2%) → LARGE
        SetupVarianceThresholdsNotConfigured();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: 90)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("LARGE");
    }

    // ─── PaddyLotId ───────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_WithPaddyLotId_PropagatedToDto()
    {
        // PaddyLotId=42 trên entity phải xuất hiện đúng trong DTO
        SetupVarianceThresholds();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: 100, paddyLotId: 42)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().PaddyLotId.Should().Be(42);
    }

    [Fact]
    public async Task GetByIdAsync_WithNullPaddyLotId_DtoReflectsNull()
    {
        // Hàng không theo lô → PaddyLotId = null
        SetupVarianceThresholds();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: 100, paddyLotId: null)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().PaddyLotId.Should().BeNull();
    }

    // ─── SoftDelete ───────────────────────────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_NotFound_Returns404()
    {
        SetupStockTakeFind(); // empty

        var result = await Sut().SoftDeleteAsync(999);

        result.Status.Should().Be(404);
    }

    [Theory]
    [InlineData(LookupCodes.StockTakeStatus.Approved)]
    [InlineData(LookupCodes.StockTakeStatus.Rejected)]
    public async Task SoftDeleteAsync_ApprovedOrRejected_Returns422(string statusCode)
    {
        var statusId = Backend.Application.Common.Lookup.StockTakeStatusId(statusCode);
        var stockTake = MakeStockTake(5);
        stockTake.StockTakeStatusId = statusId;
        SetupStockTakeFind(stockTake);

        var result = await Sut().SoftDeleteAsync(5);

        result.Status.Should().Be(422);
    }

    // ─── VariancePercent property on entity ───────────────────────────────────

    [Theory]
    [InlineData(100, 100, 0.0)]    // Không chênh lệch → VariancePercent = 0 (không phải null)
    [InlineData(100, 99,  1.0)]    // 1% chênh lệch
    [InlineData(100, 90,  10.0)]   // 10%
    [InlineData(0,   5,   null)]   // SystemQuantity=0 → null (không thể chia)
    public void StockTakeItem_VariancePercent_CalculatesCorrectly(
        decimal system, decimal actual, double? expectedPercent)
    {
        var item = new StockTakeItem
        {
            SystemQuantity = system,
            ActualQuantity = actual
        };

        if (expectedPercent == null)
            item.VariancePercent.Should().BeNull();
        else
            item.VariancePercent.Should().BeApproximately((decimal)expectedPercent, 0.01m);
    }

    [Fact]
    public void StockTakeItem_Difference_CalculatesCorrectly()
    {
        var item = new StockTakeItem { SystemQuantity = 100, ActualQuantity = 85 };
        item.Difference.Should().Be(-15);
    }

    [Fact]
    public void StockTakeItem_ActualQuantityNull_DifferenceIsNegativeSystem()
    {
        var item = new StockTakeItem { SystemQuantity = 100, ActualQuantity = null };
        item.Difference.Should().Be(-100); // (0) - 100
    }

    // ─── AbsoluteVarianceKg property on entity ────────────────────────────────

    [Theory]
    [InlineData(10000.0, 9970.0, 30.0)]  // FDS case: 30 kg, 0.3%
    [InlineData(100.0,   90.0,   10.0)]  // 10 kg
    [InlineData(100.0,   110.0,  10.0)]  // Thừa hàng: 10 kg (không âm)
    [InlineData(100.0,   null,   0.0)]   // Chưa nhập: 0
    public void StockTakeItem_AbsoluteVarianceKg_CalculatesCorrectly(
        double system, double? actual, double expectedKg)
    {
        var item = new StockTakeItem
        {
            SystemQuantity = (decimal)system,
            ActualQuantity = actual.HasValue ? (decimal?)actual.Value : null
        };
        item.AbsoluteVarianceKg.Should().BeApproximately((decimal)expectedKg, 0.001m);
    }

    // ─── RecountConfirmed trên entity ─────────────────────────────────────────

    [Fact]
    public void StockTakeItem_RecountConfirmed_DefaultIsFalse()
    {
        var item = new StockTakeItem();
        item.RecountConfirmed.Should().BeFalse();
    }

    // ─── CreateAsync / UpdateAsync Validation Tests ───────────────────────────

    [Fact]
    public async Task CreateAsync_ItemMissingProductVariantId_Returns400()
    {
        var dto = new CreateStockTakeDto
        {
            WarehouseId = 1,
            StockTakeItems = new List<CreateStockTakeItemDto>
            {
                new() { LocationId = 1, ProductVariantId = null, ActualQuantity = 10 }
            }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("ProductVariantId (Mã sản phẩm) là bắt buộc");
    }

    [Fact]
    public async Task CreateAsync_ItemMissingLocationId_Returns400()
    {
        var dto = new CreateStockTakeDto
        {
            WarehouseId = 1,
            StockTakeItems = new List<CreateStockTakeItemDto>
            {
                new() { LocationId = null, ProductVariantId = 1, ActualQuantity = 10 }
            }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("LocationId (Vị trí) là bắt buộc");
    }

    [Fact]
    public async Task CreateAsync_InvalidPaddyLotRelation_Returns400()
    {
        // PaddyLot belongs to warehouse 2, but stocktake is for warehouse 1
        var lot = new Backend.Domain.Entities.PaddyLot { Id = 10, WarehouseId = 2, ProductVariantId = 1, LocationId = 1, IsDeleted = false };
        var mockSet = new List<Backend.Domain.Entities.PaddyLot> { lot }.AsQueryable().BuildMockDbSet();
        _dbContext.Setup(c => c.PaddyLots).Returns(mockSet.Object);

        var dto = new CreateStockTakeDto
        {
            WarehouseId = 1,
            StockTakeItems = new List<CreateStockTakeItemDto>
            {
                new() { LocationId = 1, ProductVariantId = 1, PaddyLotId = 10, ActualQuantity = 10 }
            }
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("không phải kho ID 1 của phiếu kiểm kê");
    }

    [Fact]
    public async Task CreateAsync_LotLocationNullButInventoryAtSelectedLocation_IsAccepted()
    {
        var lot = new Backend.Domain.Entities.PaddyLot
        {
            Id = 94,
            WarehouseId = 1,
            ProductVariantId = 1,
            LocationId = null,
            IsDeleted = false
        };
        var inventory = new Backend.Domain.Entities.Inventory
        {
            Id = 500,
            WarehouseId = 1,
            LocationId = 7,
            ProductVariantId = 1,
            PaddyLotId = 94,
            QuantityOnHand = 120m,
            IsDeleted = false
        };
        _dbContext.Setup(c => c.PaddyLots)
            .Returns(new List<Backend.Domain.Entities.PaddyLot> { lot }.AsQueryable().BuildMockDbSet().Object);
        _dbContext.Setup(c => c.Inventories)
            .Returns(new List<Backend.Domain.Entities.Inventory> { inventory }.AsQueryable().BuildMockDbSet().Object);
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>()))
            .Returns(new List<Backend.Domain.Entities.Inventory> { inventory }.AsQueryable().BuildMock());

        var result = await Sut().CreateAsync(new CreateStockTakeDto
        {
            WarehouseId = 1,
            StockTakeItems = new List<CreateStockTakeItemDto>
            {
                new() { LocationId = 7, ProductVariantId = 1, PaddyLotId = 94 }
            }
        });

        result.Status.Should().Be(201);
    }

    /// <summary>
    /// Kiểm kê chỉ còn theo CỘT, nhưng vẫn phải loại vị trí Chờ xuất: hàng ở đó
    /// đã gắn với phiếu xuất và bao đã đóng gói nên không được điều chỉnh tay.
    /// </summary>
    [Fact]
    public async Task CreateAsync_ColumnScope_ExcludesOutboundStagingInventory()
    {
        var normalLocation = new Location
        {
            Id = 7,
            WarehouseId = 1,
            IsActive = true,
            IsOutboundStaging = false
        };
        var stagingLocation = new Location
        {
            Id = 8,
            WarehouseId = 1,
            IsActive = true,
            IsOutboundStaging = true
        };
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            new()
            {
                Id = 500,
                WarehouseId = 1,
                LocationId = normalLocation.Id,
                Location = normalLocation,
                ProductVariantId = 10,
                QuantityOnHand = 120m
            },
            new()
            {
                Id = 501,
                WarehouseId = 1,
                LocationId = stagingLocation.Id,
                Location = stagingLocation,
                ProductVariantId = 11,
                QuantityOnHand = 80m
            }
        };
        _dbContext.Setup(c => c.Inventories)
            .Returns(inventories.AsQueryable().BuildMockDbSet().Object);
        _invRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<Backend.Domain.Entities.Inventory, bool>> predicate, bool _) =>
                inventories.AsQueryable().Where(predicate.Compile()).AsQueryable().BuildMock());

        StockTake? created = null;
        _stockTakeRepo.Setup(r => r.CreateAsync(It.IsAny<StockTake>()))
            .Callback<StockTake>(value => created = value)
            .Returns(Task.CompletedTask);

        var result = await Sut().CreateAsync(new CreateStockTakeDto
        {
            WarehouseId = 1,
            ScopeType = "COLUMN",
            LocationId = normalLocation.Id
        });

        result.Status.Should().Be(201);
        created.Should().NotBeNull();
        created!.StockTakeItems.Should().ContainSingle();
        created.StockTakeItems.Single().LocationId.Should().Be(normalLocation.Id);
        created.StockTakeItems.Should().NotContain(item => item.LocationId == stagingLocation.Id);

        // Chọn thẳng vị trí Chờ xuất thì không dựng được phiếu: hàng ở đó đã gắn
        // với phiếu xuất và bao đã đóng gói, không được điều chỉnh bằng kiểm kê.
        var stagingResult = await Sut().CreateAsync(new CreateStockTakeDto
        {
            WarehouseId = 1,
            ScopeType = "COLUMN",
            LocationId = stagingLocation.Id
        });

        stagingResult.Status.Should().Be(422);
    }

    [Theory]
    [InlineData("WAREHOUSE")]
    [InlineData("ZONE")]
    [InlineData("LOT")]
    [InlineData("SKU")]
    public async Task CreateAsync_NonColumnScope_IsRejected(string scopeType)
    {
        // Kho chỉ dán QR theo cột và thủ kho dỡ hàng theo cột nên phạm vi khác
        // không còn dựng được phiếu mới.
        var result = await Sut().CreateAsync(new CreateStockTakeDto
        {
            WarehouseId = 1,
            ScopeType = scopeType,
            LocationId = 7
        });

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_ColumnScopeWithoutLocation_IsRejected()
    {
        var result = await Sut().CreateAsync(new CreateStockTakeDto
        {
            WarehouseId = 1,
            ScopeType = "COLUMN"
        });

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task UpdateAsync_RecountConfirmedAuditFieldsUpdated_WhenChangedToTrue()
    {
        // Arrange
        var stockTake = MakeStockTake(1, new StockTakeItem
        {
            Id = 100,
            StockTakeId = 1,
            ProductVariantId = 1,
            LocationId = 1,
            RecountConfirmed = false,
            RecountConfirmedBy = null,
            RecountConfirmedAt = null
        });
        SetupStockTakeFind(stockTake);

        var dto = new UpdateStockTakeDto
        {
            Id = 1,
            StockTakeStatusId = 1, // Draft
            StockTakeItems = new List<UpdateStockTakeItemDto>
            {
                new()
                {
                    Id = 100,
                    ProductVariantId = 1,
                    LocationId = 1,
                    RecountConfirmed = true // changed to true
                }
            }
        };

        // Mock HttpContext user ID via claims
        var context = new DefaultHttpContext();
        var claims = new List<System.Security.Claims.Claim>
        {
            new(Backend.Share.Constants.ClaimNames.ID, "99")
        };
        var identity = new System.Security.Claims.ClaimsIdentity(claims, "TestAuth");
        context.User = new System.Security.Claims.ClaimsPrincipal(identity);
        _http.Setup(h => h.HttpContext).Returns(context);

        // Act
        var result = await Sut().UpdateAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        var updatedItem = stockTake.StockTakeItems.First();
        updatedItem.RecountConfirmed.Should().BeTrue();
        updatedItem.RecountConfirmedBy.Should().Be(99);
        updatedItem.RecountConfirmedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task SaveCountsAsync_SubmittedPhiếu_Returns422AndDoesNotChangeEvidence()
    {
        var item = MakeItem(system: 100m, actual: null);
        var stockTake = MakeStockTake(1, item);
        stockTake.StockTakeStatusId = Backend.Application.Common.Lookup.StockTakeStatusId(
            LookupCodes.StockTakeStatus.Submitted);
        SetupStockTakeFind(stockTake);

        var result = await Sut().SaveCountsAsync(1, new SaveStockTakeCountsDto
        {
            Items = new List<SaveStockTakeCountItemDto>
            {
                new() { Id = item.Id, ActualQuantity = 95m, Note = "Sai lệch" }
            }
        }, userId: 99);

        result.Status.Should().Be(422);
        item.ActualQuantity.Should().BeNull("phiếu đã gửi duyệt phải giữ nguyên bằng chứng");
    }

    [Fact]
    public async Task SaveCountsAsync_NegativeActualQuantity_Returns400()
    {
        var item = MakeItem(system: 100m, actual: null);
        var stockTake = MakeStockTake(1, item);
        stockTake.StockTakeStatusId = Backend.Application.Common.Lookup.StockTakeStatusId(
            LookupCodes.StockTakeStatus.Draft);
        SetupStockTakeFind(stockTake);

        var result = await Sut().SaveCountsAsync(1, new SaveStockTakeCountsDto
        {
            Items = new List<SaveStockTakeCountItemDto>
            {
                new() { Id = item.Id, ActualQuantity = -1m }
            }
        }, userId: 99);

        result.Status.Should().Be(400);
        item.ActualQuantity.Should().BeNull();
    }

    [Fact]
    public async Task ApproveAsync_StagingVariance_ReturnsConflictWithoutAdjustingInventory()
    {
        var item = MakeItem(system: 100m, actual: 95m);
        item.Location = new Location
        {
            Id = item.LocationId!.Value,
            IsActive = true,
            IsOutboundStaging = true,
            SlotCode = "OUT-STAGING-1"
        };
        var stockTake = MakeStockTake(1, item);
        stockTake.StockTakeStatusId = Backend.Application.Common.Lookup.StockTakeStatusId(
            LookupCodes.StockTakeStatus.Submitted);
        stockTake.CreatedBy = 88;
        SetupStockTakeFind(stockTake);

        var context = new DefaultHttpContext();
        context.User = new System.Security.Claims.ClaimsPrincipal(
            new System.Security.Claims.ClaimsIdentity(new[]
            {
                new System.Security.Claims.Claim(Backend.Share.Constants.ClaimNames.ROLE_IDS,
                    CommonConstants.Role.ADMIN.ToString())
            }, "TestAuth"));
        _http.Setup(h => h.HttpContext).Returns(context);

        var result = await Sut().ApproveAsync(1, null, userId: 99);

        result.Status.Should().Be(409);
        result.Message.Should().Contain("Chờ xuất");
        _stockTakeRepo.Verify(r => r.BeginTransactionAsync(), Times.Never);
        _invTxService.Verify(r => r.AdjustStockAsync(
            It.IsAny<Backend.Application.DTOs.InventoryTransactions.StockMovementRequestDto>(),
            It.IsAny<decimal>(), It.IsAny<bool>()), Times.Never);
    }
}
