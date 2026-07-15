using System;
using System.Threading.Tasks;
using Backend.Application.DTOs.PartyDebts;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.PartyDebt;

/// <summary>
/// Unit tests cho PartyDebtService.
/// Tập trung vào #11: PaymentAsync không cho thanh toán vượt quá dư nợ hiện tại.
/// </summary>
[Trait("Service", "PartyDebt")]
public class PartyDebtServiceTests
{
    private readonly Mock<IPartyDebtRepository> _debtRepo = new();
    private readonly Mock<IDebtTransactionRepository> _txRepo = new();

    private PartyDebtService Sut() => new(_debtRepo.Object, _txRepo.Object);

    // ── Helper: tạo PartyDebt hợp lệ ─────────────────────────────────────────

    private static Domain.Entities.PartyDebt ActiveDebt(decimal balance = 1_000_000m) => new()
    {
        Id = 1, IsDeleted = false, IsActive = true,
        PartyType = "FARMER", PartyId = 10, Direction = "PAYABLE",
        CurrentBalance = balance, OpeningBalance = 0
    };

    // ── ChargeAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ChargeAsync_ValidAmount_IncreasesBalance()
    {
        // Arrange
        var debt = ActiveDebt(500_000m);
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);
        _debtRepo.Setup(r => r.UpdateAsync(debt)).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.CreateAsync(It.IsAny<DebtTransaction>())).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 200_000m,
            TransactionDate = DateTime.UtcNow, TransactionType = "CHARGE"
        };

        // Act
        var result = await Sut().ChargeAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        debt.CurrentBalance.Should().Be(700_000m); // 500k + 200k
    }

    [Fact]
    public async Task ChargeAsync_ZeroAmount_Returns400()
    {
        var dto = new CreateDebtTransactionDto { PartyDebtId = 1, Amount = 0m };

        var result = await Sut().ChargeAsync(dto);

        result.Status.Should().Be(400);
        _debtRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task ChargeAsync_NegativeAmount_Returns400()
    {
        var dto = new CreateDebtTransactionDto { PartyDebtId = 1, Amount = -500m };

        var result = await Sut().ChargeAsync(dto);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task ChargeAsync_DebtNotFound_Returns404()
    {
        _debtRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.PartyDebt?)null);

        var result = await Sut().ChargeAsync(new CreateDebtTransactionDto
        {
            PartyDebtId = 999, Amount = 100_000m, TransactionDate = DateTime.UtcNow
        });

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task ChargeAsync_LockedDebt_Returns422()
    {
        var debt = ActiveDebt();
        debt.IsActive = false;
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);

        var result = await Sut().ChargeAsync(new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 100_000m, TransactionDate = DateTime.UtcNow
        });

        result.Status.Should().Be(422);
    }

    // ── PaymentAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task PaymentAsync_ValidAmount_DecreasesBalance()
    {
        // Arrange
        var debt = ActiveDebt(1_000_000m);
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);
        _debtRepo.Setup(r => r.UpdateAsync(debt)).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.CreateAsync(It.IsAny<DebtTransaction>())).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 300_000m,
            TransactionDate = DateTime.UtcNow, TransactionType = "PAYMENT"
        };

        // Act
        var result = await Sut().PaymentAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        debt.CurrentBalance.Should().Be(700_000m); // 1000k - 300k
    }

    [Fact]
    public async Task PaymentAsync_ExactBalance_DecreasesToZero()
    {
        // Arrange
        var debt = ActiveDebt(500_000m);
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);
        _debtRepo.Setup(r => r.UpdateAsync(debt)).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.CreateAsync(It.IsAny<DebtTransaction>())).Returns(Task.CompletedTask);
        _txRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 500_000m,
            TransactionDate = DateTime.UtcNow
        };

        // Act
        var result = await Sut().PaymentAsync(dto);

        // Assert — số dư bằng 0 vẫn được phép
        result.Status.Should().Be(200);
        debt.CurrentBalance.Should().Be(0m);
    }

    [Fact]
    public async Task PaymentAsync_ExceedsBalance_Returns422_WithNegativeBalanceMessage()
    {
        // Arrange — dư nợ 200k nhưng thanh toán 300k → bị từ chối (#11)
        var debt = ActiveDebt(200_000m);
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);

        var dto = new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 300_000m,
            TransactionDate = DateTime.UtcNow
        };

        // Act
        var result = await Sut().PaymentAsync(dto);

        // Assert
        result.Status.Should().Be(422);
        result.Message.Should().Contain("vượt quá dư nợ hiện tại");
        debt.CurrentBalance.Should().Be(200_000m); // không được thay đổi
        _debtRepo.Verify(r => r.UpdateAsync(It.IsAny<Domain.Entities.PartyDebt>()), Times.Never);
    }

    [Fact]
    public async Task PaymentAsync_ZeroAmount_Returns400_BeforeQueryingDB()
    {
        var dto = new CreateDebtTransactionDto { PartyDebtId = 1, Amount = 0m };

        var result = await Sut().PaymentAsync(dto);

        result.Status.Should().Be(400);
        _debtRepo.Verify(r => r.GetByIdAsync(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public async Task PaymentAsync_DebtNotFound_Returns404()
    {
        _debtRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.PartyDebt?)null);

        var result = await Sut().PaymentAsync(new CreateDebtTransactionDto
        {
            PartyDebtId = 999, Amount = 100_000m, TransactionDate = DateTime.UtcNow
        });

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task PaymentAsync_LockedDebt_Returns422()
    {
        var debt = ActiveDebt(500_000m);
        debt.IsActive = false;
        _debtRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(debt);

        var result = await Sut().PaymentAsync(new CreateDebtTransactionDto
        {
            PartyDebtId = 1, Amount = 100_000m, TransactionDate = DateTime.UtcNow
        });

        result.Status.Should().Be(422);
    }

    // ── GetTransactionsAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task GetTransactionsAsync_DebtNotFound_Returns404()
    {
        _debtRepo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.PartyDebt?)null);

        var result = await Sut().GetTransactionsAsync(999, new DTParameter());

        result.Status.Should().Be(404);
    }
}
