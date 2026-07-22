using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.MillingOrders;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.MillingOrder;

/// <summary>
/// Unit tests cho MillingOrderService.
/// Tập trung: #6 Mass balance validation trong CreateAsync.
/// </summary>
[Trait("Service", "MillingOrder")]
public class MillingOrderServiceTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────
    private readonly Mock<IMillingOrderRepository>              _orderRepo    = new();
    private readonly Mock<IPaddyLotRepository>                  _paddyLotRepo = new();
    private readonly Mock<IRepositoryBase<MillingOrderInput,  int>> _inputRepo  = new();
    private readonly Mock<IRepositoryBase<MillingOrderOutput, int>> _outputRepo = new();
    private readonly Mock<IRepositoryBase<MillingOrderStatus, int>> _statusRepo = new();
    private readonly Mock<IRepositoryBase<LotStatus,          int>> _lotStatusRepo = new();
    private readonly Mock<IInventoryRepository>                 _invRepo      = new();
    private readonly Mock<IInventoryTransactionRepository>      _invTxRepo    = new();
    private readonly Mock<ILocationRepository>                  _locationRepo = new();
    private readonly Mock<INotificationDispatcher>              _dispatcher   = new();

    private MillingOrderService Sut() => new(
        _orderRepo.Object,
        _paddyLotRepo.Object,
        _inputRepo.Object,
        _outputRepo.Object,
        _statusRepo.Object,
        _lotStatusRepo.Object,
        _invRepo.Object,
        _invTxRepo.Object,
        _locationRepo.Object,
        _dispatcher.Object);

    // ── Common setup helpers ─────────────────────────────────────────────────

    /// <summary>Setup _orderRepo.FindByCondition để trả về IQueryable rỗng (count=0).</summary>
    private void SetupCodeGenCount(int count = 0)
    {
        _orderRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>()))
             .Returns(Enumerable.Empty<Domain.Entities.MillingOrder>()
                 .AsQueryable().BuildMock());
    }

    /// <summary>Setup status repo trả về Draft status.</summary>
    private void SetupDraftStatus()
    {
        var draft = new MillingOrderStatus { Id = 1, Name = "Draft", IsDeleted = false };
        _statusRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<MillingOrderStatus, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<MillingOrderStatus, object>>[]>()))
             .ReturnsAsync(draft);
    }

    // ── CreateAsync — mass balance validation (#6) ───────────────────────────

    [Fact]
    public async Task CreateAsync_OutputPlusLoss_ExceedsInput_Plus2Percent_Returns400()
    {
        // Arrange: input 100 kg, output 90 + loss 15 = 105 > 100 * 1.02 = 102
        SetupCodeGenCount();
        SetupDraftStatus();

        var dto = new CreateMillingOrderDto
        {
            WarehouseId = 1,
            Inputs  = [new() { ConsumedWeightKg = 100m }],
            Outputs = [new() { OutputWeightKg   = 90m, OutputType = "RICE" }],
            LossKg  = 15m // 90 + 15 = 105 > 102
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("Mass balance không hợp lệ");
    }

    [Fact]
    public async Task CreateAsync_OutputPlusLoss_WithinTolerance_PassesMassBalance()
    {
        // Arrange: input 100, output 95 + loss 4 = 99 ≤ 102
        SetupCodeGenCount();
        SetupDraftStatus();

        // Setup cho phép tạo thành công
        _orderRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.MillingOrder>()))
                  .Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _inputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderInput>>()))
                  .Returns(Task.CompletedTask);
        _inputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _outputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderOutput>>()))
                   .Returns(Task.CompletedTask);
        _outputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateMillingOrderDto
        {
            WarehouseId = 1,
            Inputs  = [new() { ConsumedWeightKg = 100m }],
            Outputs = [new() { OutputWeightKg   = 95m, OutputType = "RICE" }],
            LossKg  = 4m // 95 + 4 = 99 ≤ 102 → ok
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert — phải KHÔNG trả về 400 mass balance
        result.Status.Should().NotBe(400,
            because: "99 kg output+loss ≤ 100 kg input × 1.02 (tolerance 2%)");
    }

    [Fact]
    public async Task CreateAsync_OutputExactlyAtToleranceBoundary_PassesMassBalance()
    {
        // Arrange: input 100, output 102 = 100 * 1.02 (biên dung sai)
        SetupCodeGenCount();
        SetupDraftStatus();
        _orderRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.MillingOrder>()))
                  .Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _inputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderInput>>()))
                  .Returns(Task.CompletedTask);
        _inputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _outputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderOutput>>()))
                   .Returns(Task.CompletedTask);
        _outputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateMillingOrderDto
        {
            WarehouseId = 1,
            Inputs  = [new() { ConsumedWeightKg = 100m }],
            Outputs = [new() { OutputWeightKg   = 102m, OutputType = "RICE" }],
            LossKg  = 0m // exactly at boundary
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        result.Status.Should().NotBe(400,
            because: "102 = 100×1.02 nằm đúng biên — phải chấp nhận");
    }

    [Fact]
    public async Task CreateAsync_OutputJustAboveTolerance_Returns400()
    {
        // Arrange: input 100, output 102.01 > 102
        SetupCodeGenCount();
        SetupDraftStatus();

        var dto = new CreateMillingOrderDto
        {
            WarehouseId = 1,
            Inputs  = [new() { ConsumedWeightKg = 100m }],
            Outputs = [new() { OutputWeightKg   = 102.01m, OutputType = "RICE" }],
            LossKg  = 0m
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_NoInputs_SkipsMassBalanceCheck_And_Proceeds()
    {
        // Arrange: không có input → totalInputKg = 0 → skip mass balance check
        SetupCodeGenCount();
        SetupDraftStatus();
        _orderRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.MillingOrder>()))
                  .Returns(Task.CompletedTask);
        _orderRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _inputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderInput>>()))
                  .Returns(Task.CompletedTask);
        _inputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _outputRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<MillingOrderOutput>>()))
                   .Returns(Task.CompletedTask);
        _outputRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateMillingOrderDto { WarehouseId = 1, Inputs = [], Outputs = [] };

        var result = await Sut().CreateAsync(dto);

        // Không nên trả về 400 do mass balance
        result.Status.Should().NotBe(400);
    }

    [Fact]
    public async Task CreateAsync_MultipleInputs_MassBalanceUsesTotalSum()
    {
        // Arrange: 2 lô lúa: 60 + 40 = 100 kg. Output 106 > 102 → rejected
        SetupCodeGenCount();
        SetupDraftStatus();

        var dto = new CreateMillingOrderDto
        {
            WarehouseId = 1,
            Inputs =
            [
                new() { ConsumedWeightKg = 60m },
                new() { ConsumedWeightKg = 40m }
            ],
            Outputs = [new() { OutputWeightKg = 106m, OutputType = "RICE" }],
            LossKg  = 0m
        };

        var result = await Sut().CreateAsync(dto);

        result.Status.Should().Be(400);
        result.Message.Should().Contain("Mass balance không hợp lệ");
    }

    // ── GetAllAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_ReturnsSuccess_WithEmptyList()
    {
        _orderRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.MillingOrder, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.MillingOrder, object>>[]>()))
             .Returns(new List<Domain.Entities.MillingOrder>().AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }

    // ── NotImplemented bulk operations → 501 ─────────────────────────────────

    [Fact]
    public async Task CreateListAsync_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreateMillingOrderDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task UpdateListAsync_Returns501()
    {
        var result = await Sut().UpdateListAsync(new List<UpdateMillingOrderDto>());
        result.Status.Should().Be(501);
    }
}
