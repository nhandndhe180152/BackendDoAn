using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Backend.Application.Constants;
using Backend.Application.Interfaces;
using Backend.Infrastructure.Persistence;
using Backend.Share.Services;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Alert = Backend.Domain.Entities.Alert;
using PartyDebt = Backend.Domain.Entities.PartyDebt;
using DebtTransaction = Backend.Domain.Entities.DebtTransaction;
using Farmer = Backend.Domain.Entities.Farmer;
using Customer = Backend.Domain.Entities.Customer;
using Supplier = Backend.Domain.Entities.Supplier;
using SystemConfig = Backend.Domain.Entities.SystemConfig;

namespace Backend.UnitTest.BackgroundJobs.DebtDueOverdue;

public class DebtDueAndOverdueReminderTests
{
    private readonly Mock<INotificationDispatcher> _dispatcherMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IDataChangeNotifier> _realtimeMock = new();
    private readonly Mock<ILoggerFactory> _loggerFactoryMock = new();
    private readonly Mock<ILogger<DebtDueAndOverdueReminderService>> _loggerMock = new();

    public DebtDueAndOverdueReminderTests()
    {
        _loggerFactoryMock
            .Setup(x => x.CreateLogger(It.IsAny<string>()))
            .Returns(_loggerMock.Object);
    }

    private BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private async Task SeedBaseDataAsync(BackendContext context)
    {
        // Seed default config thresholds
        var configs = new List<SystemConfig>
        {
            new() { Id = 1, ConfigKey = DebtDueOverdueConstants.ConfigKey.LeadDays, ConfigValue = "7", Name = "LeadDays" },
            new() { Id = 2, ConfigKey = DebtDueOverdueConstants.ConfigKey.DueTodaySeverity, ConfigValue = "WARNING", Name = "DueTodaySeverity" },
            new() { Id = 3, ConfigKey = DebtDueOverdueConstants.ConfigKey.OverdueWarningDays, ConfigValue = "1", Name = "OverdueWarningDays" },
            new() { Id = 4, ConfigKey = DebtDueOverdueConstants.ConfigKey.OverdueCriticalDays, ConfigValue = "30", Name = "OverdueCriticalDays" },
            new() { Id = 5, ConfigKey = DebtDueOverdueConstants.ConfigKey.OverdueWarningAmount, ConfigValue = "10000000", Name = "OverdueWarningAmount" },
            new() { Id = 6, ConfigKey = DebtDueOverdueConstants.ConfigKey.OverdueCriticalAmount, ConfigValue = "50000000", Name = "OverdueCriticalAmount" }
        };
        context.SystemConfigs.AddRange(configs);

        // Seed Parties
        context.Farmers.Add(new Farmer { Id = 1, Code = "F-01", Name = "Farmer Nguyen Van A", IsActive = true });
        context.Customers.Add(new Customer { Id = 1, Code = "C-01", Name = "Customer Nguyen Van B", IsActive = true });
        context.Suppliers.Add(new Supplier { Id = 1, Code = "S-01", Name = "Supplier Nguyen Van C", IsActive = true });

        await context.SaveChangesAsync();
    }

    [Fact]
    public void CalculatePartyDebtAging_PayableAndReceivable_CalculatesCorrectOutstandingAndBuckets()
    {
        // Arrange
        var effectResolver = new DebtTransactionEffectResolver();
        var calcService = new DebtAgingCalculationService(null!, effectResolver);

        var partyDebt = new PartyDebt
        {
            Id = 1,
            PartyType = "FARMER",
            PartyId = 1,
            Direction = "PAYABLE",
            OpeningBalance = 0m,
            CurrentBalance = 15000000m,
            IsActive = true
        };

        var today = new DateTime(2026, 7, 24);

        // 3 charges:
        // 1. Overdue: 10M, DueDate = 2026-07-10 (14 days overdue)
        // 2. Due today: 3M, DueDate = 2026-07-24
        // 3. Due soon: 5M, DueDate = 2026-07-28 (4 days until due, within lead days 7)
        // Total charges: 18M. Total payments/credits: 3M (which will reduce the oldest charge by 3M).
        var transactions = new List<DebtTransaction>
        {
            new() { Id = 1, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 10000000m, DueDate = new DateTime(2026, 7, 10), TransactionDate = new DateTime(2026, 7, 10) },
            new() { Id = 2, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 3000000m, DueDate = new DateTime(2026, 7, 24), TransactionDate = new DateTime(2026, 7, 24) },
            new() { Id = 3, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 5000000m, DueDate = new DateTime(2026, 7, 28), TransactionDate = new DateTime(2026, 7, 28) },
            new() { Id = 4, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Payment, Amount = 3000000m, TransactionDate = new DateTime(2026, 7, 15) }
        };

        // Act
        var result = calcService.CalculatePartyDebtAging(partyDebt, transactions, today, 7);

        // Assert
        result.CurrentBalance.Should().Be(15000000m);
        // Oldest charge (10M) had 3M payment applied -> 7M remains overdue.
        result.OverdueAmount.Should().Be(7000000m);
        result.DueTodayAmount.Should().Be(3000000m);
        result.DueSoonAmount.Should().Be(5000000m);
        result.NotYetDueAmount.Should().Be(0m);
        result.OldestOverdueDate.Should().Be(new DateTime(2026, 7, 10));
        result.MaxDaysOverdue.Should().Be(14);
        result.OpenChargeCount.Should().Be(3);
        result.ReconciliationDifference.Should().Be(0m);
    }

    [Fact]
    public void CalculatePartyDebtAging_PaymentGreaterClassified_DistributesAcrossMultipleCharges()
    {
        // Arrange
        var effectResolver = new DebtTransactionEffectResolver();
        var calcService = new DebtAgingCalculationService(null!, effectResolver);

        var partyDebt = new PartyDebt
        {
            Id = 1,
            PartyType = "CUSTOMER",
            PartyId = 1,
            Direction = "RECEIVABLE",
            OpeningBalance = 0m,
            CurrentBalance = 4000000m,
            IsActive = true
        };

        var today = new DateTime(2026, 7, 24);

        // 3 charges: 5M, 5M, 5M (Total 15M)
        // Payment: 11M (reduces first 5M completely, second 5M completely, third 5M has 1M applied -> 4M remains)
        var transactions = new List<DebtTransaction>
        {
            new() { Id = 1, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 5000000m, DueDate = new DateTime(2026, 7, 10), TransactionDate = new DateTime(2026, 7, 10) },
            new() { Id = 2, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 5000000m, DueDate = new DateTime(2026, 7, 15), TransactionDate = new DateTime(2026, 7, 15) },
            new() { Id = 3, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Charge, Amount = 5000000m, DueDate = new DateTime(2026, 7, 20), TransactionDate = new DateTime(2026, 7, 20) },
            new() { Id = 4, PartyDebtId = 1, TransactionType = LookupCodes.DebtTransactionType.Payment, Amount = 11000000m, TransactionDate = new DateTime(2026, 7, 21) }
        };

        // Act
        var result = calcService.CalculatePartyDebtAging(partyDebt, transactions, today, 7);

        // Assert
        result.CurrentBalance.Should().Be(4000000m);
        // Only the last charge (DueDate 2026-07-20) remains outstanding (4M overdue)
        result.OverdueAmount.Should().Be(4000000m);
        result.OldestOverdueDate.Should().Be(new DateTime(2026, 7, 20));
        result.MaxDaysOverdue.Should().Be(4);
        result.OpenChargeCount.Should().Be(1);
    }

    [Fact]
    public void CalculateDebtDocuments_TargetedPayment_PaysSelectedDocumentBeforeFifo()
    {
        var calcService = new DebtAgingCalculationService(
            null!,
            new DebtTransactionEffectResolver());
        var debt = new PartyDebt
        {
            Id = 1,
            PartyType = "CUSTOMER",
            PartyId = 1,
            Direction = "RECEIVABLE",
            CurrentBalance = 12000000m,
            IsActive = true
        };
        var transactions = new List<DebtTransaction>
        {
            new() { Id = 1, PartyDebtId = 1, TransactionType = "CHARGE", Amount = 10000000m, RefType = "OUTBOUND_ORDER", RefId = 10, TransactionDate = new DateTime(2026, 7, 1), DueDate = new DateTime(2026, 7, 10) },
            new() { Id = 2, PartyDebtId = 1, TransactionType = "CHARGE", Amount = 10000000m, RefType = "OUTBOUND_ORDER", RefId = 20, TransactionDate = new DateTime(2026, 7, 2), DueDate = new DateTime(2026, 7, 11) },
            new() { Id = 3, PartyDebtId = 1, TransactionType = "PAYMENT", Amount = 8000000m, RefType = "OUTBOUND_ORDER", RefId = 20, TransactionDate = new DateTime(2026, 7, 3) }
        };

        var documents = calcService.CalculateDebtDocuments(debt, transactions);

        documents.Single(x => x.RefId == 10).OutstandingAmount.Should().Be(10000000m);
        documents.Single(x => x.RefId == 20).OutstandingAmount.Should().Be(2000000m);
        documents.Sum(x => x.OutstandingAmount).Should().Be(debt.CurrentBalance);
    }

    [Fact]
    public void EvaluateRules_DueSoonAndOverdue_CorrectSeverityAndMetadata()
    {
        // Arrange
        var rulesEngine = new DebtDueOverdueRulesEngine();

        var partyDebt = new PartyDebt
        {
            Id = 1,
            PartyType = "FARMER",
            PartyId = 1,
            Direction = "PAYABLE",
            OpeningBalance = 0m,
            CurrentBalance = 60000000m,
            IsActive = true
        };

        var agingResult = new DebtAgingCalculationResult
        {
            PartyDebtId = 1,
            CurrentBalance = 60000000m,
            DueSoonAmount = 5000000m,
            DueTodayAmount = 0m,
            OverdueAmount = 55000000m, // Overdue critical amount threshold is 50M
            OldestOverdueDate = new DateTime(2026, 6, 20),
            MaxDaysOverdue = 34, // Overdue critical days threshold is 30
            OpenChargeCount = 2
        };

        var config = new DebtDueOverdueConfig
        {
            PartyDebtId = 1,
            Direction = "PAYABLE",
            ReminderLeadDays = 7,
            DueTodaySeverity = "WARNING",
            OverdueWarningDays = 1,
            OverdueCriticalDays = 30,
            OverdueWarningAmount = 10000000m,
            OverdueCriticalAmount = 50000000m
        };

        var today = new DateTime(2026, 7, 24);

        // Act
        var evals = rulesEngine.Evaluate(partyDebt, "Farmer Nguyen Van A", agingResult, config, new List<string>(), today);

        // Assert
        evals.Should().HaveCount(2);

        var dueEval = evals.Single(e => e.AlertType == DebtDueOverdueConstants.AlertType.DebtDueSoon);
        dueEval.ShouldAlert.Should().BeTrue();
        dueEval.Severity.Should().Be(AlertConstants.Severity.Info);
        dueEval.Message.Should().Contain("Farmer Nguyen Van A").And.Contain("5.000.000");

        var overdueEval = evals.Single(e => e.AlertType == DebtDueOverdueConstants.AlertType.DebtOverdue);
        overdueEval.ShouldAlert.Should().BeTrue();
        overdueEval.Severity.Should().Be(AlertConstants.Severity.Critical); // Both amount and days critical thresholds exceeded
        overdueEval.Message.Should().Contain("quá hạn 34 ngày").And.Contain("55.000.000");
    }

    [Fact]
    public async Task EvaluateAllDebtsAsync_DebtResolvedToZero_ResolvesAlertsIdempotently()
    {
        // Arrange
        using var context = CreateContext();
        await SeedBaseDataAsync(context);

        // Active debt with 0 balance (which means it's fully settled)
        var debt = new PartyDebt
        {
            Id = 1,
            PartyType = "CUSTOMER",
            PartyId = 1,
            Direction = "RECEIVABLE",
            OpeningBalance = 0m,
            CurrentBalance = 0m,
            IsActive = true,
            IsDeleted = false
        };
        context.PartyDebts.Add(debt);

        // Existing open alert for this debt
        var existingAlert = new Alert
        {
            Id = 101,
            AlertType = DebtDueOverdueConstants.AlertType.DebtOverdue,
            Severity = AlertConstants.Severity.Warning,
            Status = AlertConstants.Status.Open,
            RelatedEntityType = AlertConstants.RelatedEntityType.PartyDebt,
            RelatedEntityId = 1,
            Message = "Unpaid debt",
            DeduplicationKey = $"JOB04:{AlertConstants.RelatedEntityType.PartyDebt}:1:DEBT_OVERDUE",
            IsDeleted = false
        };
        context.Alerts.Add(existingAlert);
        await context.SaveChangesAsync();

        var resolver = new DebtTransactionEffectResolver();
        var calcService = new DebtAgingCalculationService(context, resolver);
        var rulesEngine = new DebtDueOverdueRulesEngine();

        var service = new DebtDueAndOverdueReminderService(
            context,
            calcService,
            rulesEngine,
            _cacheMock.Object,
            _dispatcherMock.Object,
            _realtimeMock.Object,
            _loggerFactoryMock.Object);

        // Act
        var jobResult = await service.EvaluateAllDebtsAsync(CancellationToken.None);

        // Assert
        jobResult.Processed.Should().Be(0); // 0 balance debt is skipped during scan but alerts get resolved in ResolveClearedAlertsAsync
        var alert = await context.Alerts.SingleAsync(a => a.Id == 101);
        alert.Status.Should().Be(AlertConstants.Status.Resolved);
        alert.DeduplicationKey.Should().BeNull();
    }
}
