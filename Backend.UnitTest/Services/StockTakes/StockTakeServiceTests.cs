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

    private StockTakeService Sut() => new(
        _stockTakeRepo.Object, _stockTakeItemRepo.Object, _invTxService.Object,
        _invRepo.Object, _sysConfigRepo.Object, _http.Object, _dispatcher.Object);

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>Setup SystemConfig mock trả về ngưỡng SMALL / MEDIUM từ DB.</summary>
    private void SetupVarianceThresholds(decimal small = 1m, decimal medium = 5m)
    {
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeSmallVariancePercent))
            .ReturnsAsync(small.ToString());
        _sysConfigRepo
            .Setup(r => r.GetValueByKey(SystemConfigConstants.Keys.StockTakeMediumVariancePercent))
            .ReturnsAsync(medium.ToString());
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

    // ─── ClassifySeverity qua GetByIdAsync ────────────────────────────────────

    [Theory]
    [InlineData(100, 100, "NONE")]    // Không chênh lệch → NONE
    [InlineData(100, 99.5, "SMALL")] // 0.5% ≤ 1% → SMALL
    [InlineData(100, 98, "MEDIUM")]  // 2% ≤ 5% → MEDIUM
    [InlineData(100, 90, "LARGE")]   // 10% > 5% → LARGE
    [InlineData(100, 110, "LARGE")]  // Thừa 10% → LARGE
    public async Task GetByIdAsync_VarianceSeverity_ClassifiesCorrectly(
        decimal system, decimal actual, string expectedSeverity)
    {
        // Arrange
        SetupVarianceThresholds(small: 1m, medium: 5m);
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system, actual)));

        // Act
        var result = await Sut().GetByIdAsync(1);

        // Assert
        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be(expectedSeverity);
    }

    [Fact]
    public async Task GetByIdAsync_SystemQuantityZero_ActualQuantityPositive_VarianceSeverityIsLarge()
    {
        // SystemQuantity = 0 và ActualQuantity = 10 → chênh lệch nghiêm trọng → LARGE (MEDIUM-1 fix)
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
        // 3% chênh lệch → với default (SMALL=1%, MEDIUM=5%) → MEDIUM
        SetupVarianceThresholdsNotConfigured();
        SetupStockTakeFind(MakeStockTake(1, MakeItem(system: 100, actual: 97)));

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(200);
        var dto = (result.Resources as StockTakeDto)!;
        dto.StockTakeItems.First().VarianceSeverity.Should().Be("MEDIUM");
    }

    [Fact]
    public async Task GetByIdAsync_ConfigNotInDb_FallsBackToDefaults_ClassifiesLarge()
    {
        // 10% chênh lệch → với default (SMALL=1%, MEDIUM=5%) → LARGE
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
    [InlineData(100, 99, 1.0)]     // 1% chênh lệch
    [InlineData(100, 90, 10.0)]    // 10%
    [InlineData(0, 5, null)]       // SystemQuantity=0 → null (không thể chia)
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
}
