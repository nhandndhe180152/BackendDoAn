using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.PaddyPurchaseSchedule;

/// <summary>
/// Unit tests cho PaddyPurchaseScheduleService.
/// Tập trung kiểm tra UpdateStatusAsync (#9) — validate statusCode qua ISystemLookup.
/// </summary>
[Trait("Service", "PaddyPurchaseSchedule")]
public class PaddyPurchaseScheduleServiceTests
{
    private readonly Mock<IPaddyPurchaseScheduleRepository> _repo = new();
    private readonly Mock<IPaddyPurchaseReceiptRepository> _receiptRepo = new();
    private readonly Mock<ISystemLookup> _lookup = new();
    private readonly Mock<INotificationDispatcher> _dispatcher = new();

    private PaddyPurchaseScheduleService Sut() =>
        new(_repo.Object, _receiptRepo.Object, _lookup.Object, _dispatcher.Object);

    // ── UpdateStatusAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateStatusAsync_ValidCode_UpdatesEntityAndReturns200()
    {
        // Arrange
        var entity = new Domain.Entities.PaddyPurchaseSchedule
        {
            Id = 1, StatusId = 1, IsDeleted = false
        };
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(entity);
        _lookup.Setup(l => l.PaddyScheduleStatusId("STOCKED")).Returns(5);
        _repo.Setup(r => r.UpdateAsync(entity)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await Sut().UpdateStatusAsync(1, "STOCKED", updatedBy: 99);

        // Assert
        result.Status.Should().Be(200);
        entity.StatusId.Should().Be(5);
        entity.UpdatedBy.Should().Be(99);
        _repo.Verify(r => r.UpdateAsync(entity), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateStatusAsync_InvalidCode_Returns400_WithMessage()
    {
        // Arrange — ISystemLookup ném InvalidOperationException khi không tìm thấy code
        var entity = new Domain.Entities.PaddyPurchaseSchedule { Id = 2, IsDeleted = false };
        _repo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(entity);
        _lookup.Setup(l => l.PaddyScheduleStatusId("INVALID"))
               .Throws(new InvalidOperationException(
                   "Không tìm thấy PaddyPurchaseScheduleStatus có Code = 'INVALID'."));

        // Act
        var result = await Sut().UpdateStatusAsync(2, "INVALID", updatedBy: 1);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("INVALID");
        _repo.Verify(r => r.UpdateAsync(It.IsAny<Domain.Entities.PaddyPurchaseSchedule>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_ScheduleNotFound_Returns404()
    {
        // Arrange
        _repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.PaddyPurchaseSchedule?)null);

        // Act
        var result = await Sut().UpdateStatusAsync(999, "STOCKED", updatedBy: 1);

        // Assert
        result.Status.Should().Be(404);
        _lookup.Verify(l => l.PaddyScheduleStatusId(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task UpdateStatusAsync_SoftDeletedSchedule_Returns404()
    {
        // Arrange
        var deleted = new Domain.Entities.PaddyPurchaseSchedule { Id = 3, IsDeleted = true };
        _repo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(deleted);

        // Act
        var result = await Sut().UpdateStatusAsync(3, "STOCKED", updatedBy: 1);

        // Assert
        result.Status.Should().Be(404);
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsSuccess_WithEmptyList()
    {
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyPurchaseSchedule, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.PaddyPurchaseSchedule, object>>[]>()))
             .Returns(new List<Domain.Entities.PaddyPurchaseSchedule>()
                 .AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.PaddyPurchaseSchedule, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.PaddyPurchaseSchedule, object>>[]>()))
             .Returns(new List<Domain.Entities.PaddyPurchaseSchedule>()
                 .AsQueryable().BuildMock());

        var result = await Sut().GetByIdAsync(999);

        result.Status.Should().Be(404);
    }

    // ── SoftDeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_Returns400_WhenNotFound()
    {
        _repo.Setup(r => r.SoftDeleteAsync(999)).ReturnsAsync(false);

        var result = await Sut().SoftDeleteAsync(999);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task SoftDeleteAsync_Returns200_WhenDeleted()
    {
        _repo.Setup(r => r.SoftDeleteAsync(1)).ReturnsAsync(true);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await Sut().SoftDeleteAsync(1);

        result.Status.Should().Be(200);
    }

    // ── NotImplemented bulk operations → 501 ──────────────────────────────────

    [Fact]
    public async Task CreateListAsync_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreatePaddyPurchaseScheduleDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task UpdateListAsync_Returns501()
    {
        var result = await Sut().UpdateListAsync(new List<UpdatePaddyPurchaseScheduleDto>());
        result.Status.Should().Be(501);
    }
}
