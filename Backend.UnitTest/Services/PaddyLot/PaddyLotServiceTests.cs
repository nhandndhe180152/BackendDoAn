using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.PaddyLot;

/// <summary>
/// Unit tests cho PaddyLotService.
/// Kiểm tra: GetAll, GetById, UpdateAsync, SoftDelete, bulk stubs → 501.
/// </summary>
[Trait("Service", "PaddyLot")]
public class PaddyLotServiceTests
{
    private readonly Mock<IPaddyLotRepository> _repo = new();

    private PaddyLotService Sut() => new(_repo.Object);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Domain.Entities.PaddyLot MakeLot(int id, bool deleted = false) => new()
    {
        Id = id, LotCode = $"LOT-PADDY-20260101-{id:D4}",
        LotType = "PADDY", IsDeleted = deleted,
        InitialWeightKg = 1000m, RemainingWeightKg = 1000m,
        CostPricePerKg = 8000m, InboundDate = DateTime.UtcNow,
        ProductVariantId = 1, WarehouseId = 1, StatusId = 1
    };

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsList_WhenLotsExist()
    {
        var lots = new List<Domain.Entities.PaddyLot> { MakeLot(1), MakeLot(2) };
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, bool>>>(),
                It.IsAny<bool>()))
             .Returns(lots.AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoLots()
    {
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, bool>>>(),
                It.IsAny<bool>()))
             .Returns(new List<Domain.Entities.PaddyLot>().AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_Returns404_WhenNotFound()
    {
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, object>>[]>()))
             .Returns(new List<Domain.Entities.PaddyLot>().AsQueryable().BuildMock());

        var result = await Sut().GetByIdAsync(999);

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsOk_WhenLotExists()
    {
        var lot = MakeLot(5);
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.PaddyLot, object>>[]>()))
             .Returns(new List<Domain.Entities.PaddyLot> { lot }.AsQueryable().BuildMock());

        var result = await Sut().GetByIdAsync(5);

        result.Status.Should().Be(200);
    }

    // ── UpdateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_Returns404_WhenLotNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.PaddyLot?)null);

        var result = await Sut().UpdateAsync(new UpdatePaddyLotDto { Id = 999 });

        result.Status.Should().Be(404);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Domain.Entities.PaddyLot>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_Returns404_WhenLotIsDeleted()
    {
        _repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(MakeLot(3, deleted: true));

        var result = await Sut().UpdateAsync(new UpdatePaddyLotDto { Id = 3 });

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task UpdateAsync_ReturnsOk_WhenLotExists()
    {
        var lot = MakeLot(1);
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lot);
        _repo.Setup(r => r.UpdateAsync(lot)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await Sut().UpdateAsync(new UpdatePaddyLotDto { Id = 1, LotType = "PADDY" });

        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(lot), Times.Once);
    }

    // ── SoftDeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_Returns400_WhenLotNotFound()
    {
        _repo.Setup(r => r.SoftDeleteAsync(999)).ReturnsAsync(false);

        var result = await Sut().SoftDeleteAsync(999);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task SoftDeleteAsync_ReturnsOk_WhenDeleted()
    {
        _repo.Setup(r => r.SoftDeleteAsync(1)).ReturnsAsync(true);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await Sut().SoftDeleteAsync(1);

        result.Status.Should().Be(200);
    }

    // ── Bulk stubs → 501 ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreatePaddyLotDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task UpdateListAsync_Returns501()
    {
        var result = await Sut().UpdateListAsync(new List<UpdatePaddyLotDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task GetPagedAsync_SearchQuery_Returns501()
    {
        var result = await Sut().GetPagedAsync(new SearchQuery());
        result.Status.Should().Be(501);
    }
}
