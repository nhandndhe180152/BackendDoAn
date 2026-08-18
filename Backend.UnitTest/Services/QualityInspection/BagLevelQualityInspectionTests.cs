using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using PaddyLotEntity = Backend.Domain.Entities.PaddyLot;

namespace Backend.UnitTest.Services.QualityInspection;

[Trait("Category", "BagLevelQC")]
public class BagLevelQualityInspectionTests
{
    private static BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .ConfigureWarnings(x => x.Ignore(
                Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new BackendContext(options);
    }

    private static (QualityInspectionService sut, BackendContext context) CreateService()
    {
        var context = CreateContext();
        var uow = Mock.Of<IUnitOfWork>();
        var repo = new QualityInspectionRepository(context, uow);
        var lotRepo = new PaddyLotRepository(context, uow);
        var inventoryRepo = Mock.Of<IInventoryRepository>();
        var inventoryTxRepo = Mock.Of<IInventoryTransactionRepository>();
        var lotStatusRepo = Mock.Of<IRepositoryBase<LotStatus, int>>();
        var notificationDispatcher = Mock.Of<INotificationDispatcher>();

        var sut = new QualityInspectionService(
            repo,
            lotRepo,
            inventoryRepo,
            inventoryTxRepo,
            lotStatusRepo,
            context,
            notificationDispatcher);

        return (sut, context);
    }

    private static async Task<(int inspectionId, int lotId, List<int> bagIds)> SetupBaselineReceivingLotWith3BagsAsync(BackendContext context)
    {
        int lotId = 150;
        int inspectionId = 64;

        var lot = new PaddyLotEntity
        {
            Id = lotId,
            LotCode = "LOT-PADDY-20260818-0001",
            LotType = "PADDY",
            WarehouseId = 1,
            StatusId = 1,
            IsDeleted = false
        };
        context.PaddyLots.Add(lot);

        var bags = new List<PaddyLotBag>
        {
            new() { Id = 1001, LotId = lotId, BagNo = 1, WeightKg = 49.8m, Status = PaddyLotBagStatuses.Pending, BagKind = PaddyLotBagKinds.Purchase, IsDeleted = false },
            new() { Id = 1002, LotId = lotId, BagNo = 2, WeightKg = 50.1m, Status = PaddyLotBagStatuses.Pending, BagKind = PaddyLotBagKinds.Purchase, IsDeleted = false },
            new() { Id = 1003, LotId = lotId, BagNo = 3, WeightKg = 49.9m, Status = PaddyLotBagStatuses.Pending, BagKind = PaddyLotBagKinds.Purchase, IsDeleted = false }
        };
        context.PaddyLotBags.AddRange(bags);

        var inspection = new Backend.Domain.Entities.QualityInspection
        {
            Id = inspectionId,
            PaddyLotId = lotId,
            PaddyLot = lot,
            InspectionType = InspectionTypeConstants.Receiving,
            InspectedAt = DateTime.UtcNow,
            CompletedAt = null,
            CompletedBy = null,
            IsDeleted = false
        };
        context.QualityInspections.Add(inspection);

        await context.SaveChangesAsync();

        return (inspectionId, lotId, bags.Select(b => b.Id).ToList());
    }

    [Fact(DisplayName = "C-01 / D-01: GET /bags ban đầu trả về đúng 3 bao, 0 inspected, 3 remaining")]
    public async Task D01_GetBagProgress_InitialState_ShouldReturnAll3BagsPending()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, _) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        var res = await sut.GetBagProgressAsync(inspectionId);

        res.Status.Should().Be(200);
        var progress = res.Resources as QualityInspectionBagProgressDto;
        progress.Should().NotBeNull();
        progress!.InspectionId.Should().Be(64);
        progress.InspectionType.Should().Be(InspectionTypeConstants.Receiving);
        progress.TotalBags.Should().Be(3);
        progress.InspectedBags.Should().Be(0);
        progress.RemainingBags.Should().Be(3);
        progress.NormalBags.Should().Be(0);
        progress.QuarantineBags.Should().Be(0);
        progress.RejectedBags.Should().Be(0);
        progress.Items.Should().HaveCount(3);
    }

    [Fact(DisplayName = "D-02 / D-03: Save bao #1 PASS -> ACCEPT_NORMAL và kiểm tra progress")]
    public async Task D02_D03_SaveBag1Pass_ShouldUpdateProgressCounters()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        var saveDto = new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            MoisturePercent = 12.0m,
            ImpurityPercent = 1.0m,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal,
            PackagingStatus = "GOOD",
            Note = "Bao số 1 đạt chất lượng"
        };

        var saveRes = await sut.SaveBagResultAsync(inspectionId, saveDto);
        saveRes.Status.Should().Be(200);

        var progressRes = await sut.GetBagProgressAsync(inspectionId);
        var progress = progressRes.Resources as QualityInspectionBagProgressDto;
        progress!.TotalBags.Should().Be(3);
        progress.InspectedBags.Should().Be(1);
        progress.NormalBags.Should().Be(1);
        progress.RemainingBags.Should().Be(2);

        var bag1 = progress.Items.First(b => b.BagId == bagIds[0]);
        bag1.QualityResult.Should().Be(BagQualityResultConstants.Pass);
        bag1.Disposition.Should().Be(BagDispositionConstants.AcceptNormal);
        bag1.MoisturePercent.Should().Be(12.0m);
    }

    [Fact(DisplayName = "C-02 / D-04: Test UPSERT cùng một bao - cập nhật dữ liệu, không tăng số lượng bản ghi")]
    public async Task C02_D04_UpsertSameBag_ShouldUpdateExistingResultWithoutDuplicate()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        // Save lần 1
        await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            MoisturePercent = 12.0m,
            ImpurityPercent = 1.0m,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal
        });

        // Save lần 2 (đổi moisture và note)
        var save2Res = await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            MoisturePercent = 12.5m,
            ImpurityPercent = 1.0m,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal,
            Note = "Đo lại bao số 1"
        });
        save2Res.Status.Should().Be(200);

        context.QualityInspectionBagResults.Count(r => r.QualityInspectionId == inspectionId && r.BagId == bagIds[0]).Should().Be(1);

        var progressRes = await sut.GetBagProgressAsync(inspectionId);
        var progress = progressRes.Resources as QualityInspectionBagProgressDto;
        progress!.InspectedBags.Should().Be(1);
        var bag1 = progress.Items.First(b => b.BagId == bagIds[0]);
        bag1.MoisturePercent.Should().Be(12.5m);
        bag1.Note.Should().Be("Đo lại bao số 1");
    }

    [Fact(DisplayName = "E-01 / E-02: Complete khi chưa kiểm đủ 100% bao -> trả 422, không có side-effect")]
    public async Task E01_E02_CompleteIncomplete_ShouldReturn422AndNoSideEffect()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        // Mới kiểm 1/3 bao
        await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal
        });

        var completeRes = await sut.CompleteAsync(inspectionId, new CompleteInspectionDto { Note = "Thử complete" });
        completeRes.Status.Should().Be(422);
        completeRes.Message.Should().Contain("Còn 2 bao chưa được kiểm tra");

        var inspection = await context.QualityInspections.FirstAsync(x => x.Id == inspectionId);
        inspection.CompletedAt.Should().BeNull();
        inspection.CompletedBy.Should().BeNull();
    }

    [Fact(DisplayName = "D-05: Validation Receiving PASS nhưng chọn ACCEPT_QUARANTINE -> 400 Bad Request")]
    public async Task D05_ReceivingPassWithQuarantine_ShouldReturn400()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        var res = await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[1],
            MoisturePercent = 14.0m,
            ImpurityPercent = 2.0m,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptQuarantine
        });

        res.Status.Should().Be(400);
        res.Message.Should().Contain("Receiving PASS chỉ chấp nhận Disposition = ACCEPT_NORMAL");
    }

    [Fact(DisplayName = "D-06: Validation Receiving ISSUE_DETECTED nhưng chọn ACCEPT_NORMAL -> 400 Bad Request")]
    public async Task D06_ReceivingIssueWithAcceptNormal_ShouldReturn400()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        var res = await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[1],
            QualityResult = BagQualityResultConstants.IssueDetected,
            Disposition = BagDispositionConstants.AcceptNormal
        });

        res.Status.Should().Be(400);
        res.Message.Should().Contain("Receiving ISSUE_DETECTED phải chọn ACCEPT_QUARANTINE hoặc REJECT_RETURN");
    }

    [Fact(DisplayName = "D-14: Lưu bao không thuộc inspection -> 400 Bad Request")]
    public async Task D14_BagNotBelongToInspection_ShouldReturn400()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, _) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        // Tạo 1 bao thuộc lô khác
        context.PaddyLotBags.Add(new PaddyLotBag { Id = 9999, LotId = 999, BagNo = 99, IsDeleted = false });
        await context.SaveChangesAsync();

        var res = await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = 9999,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal
        });

        res.Status.Should().Be(400);
        res.Message.Should().Contain("Bao không thuộc lô của phiếu kiểm tra này");
    }

    [Fact(DisplayName = "Full End-to-End: E-03 đến E-09 — Complete 100%, Aggregation, Side-effects, Lock và Idempotency")]
    public async Task FullE2E_CompleteReceivingWorkflow_AllCasesVerified()
    {
        var (sut, context) = CreateService();
        var (inspectionId, _, bagIds) = await SetupBaselineReceivingLotWith3BagsAsync(context);

        // 1. Lưu Bao #1: 49.8 kg, Moisture 12.5, Impurity 1.0, PASS -> ACCEPT_NORMAL
        await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            MoisturePercent = 12.5m,
            ImpurityPercent = 1.0m,
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal
        });

        // 2. Lưu Bao #2: 50.1 kg, Moisture 14.0, Impurity 2.0, ISSUE -> ACCEPT_QUARANTINE
        await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[1],
            MoisturePercent = 14.0m,
            ImpurityPercent = 2.0m,
            QualityResult = BagQualityResultConstants.IssueDetected,
            Disposition = BagDispositionConstants.AcceptQuarantine
        });

        // 3. Lưu Bao #3: 49.9 kg, Moisture 16.0, Impurity 3.0, ISSUE -> REJECT_RETURN
        await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[2],
            MoisturePercent = 16.0m,
            ImpurityPercent = 3.0m,
            QualityResult = BagQualityResultConstants.IssueDetected,
            Disposition = BagDispositionConstants.RejectReturn
        });

        // D-13: Kiểm tra progress 3/3
        var progressRes = await sut.GetBagProgressAsync(inspectionId);
        var progress = progressRes.Resources as QualityInspectionBagProgressDto;
        progress!.TotalBags.Should().Be(3);
        progress.InspectedBags.Should().Be(3);
        progress.RemainingBags.Should().Be(0);
        progress.NormalBags.Should().Be(1);
        progress.QuarantineBags.Should().Be(1);
        progress.RejectedBags.Should().Be(1);

        // E-03: Gọi Complete thành công
        var completeRes = await sut.CompleteAsync(inspectionId, new CompleteInspectionDto
        {
            Note = "Hoàn tất test Bag-level Receiving QC",
            CompletedBy = 1001
        });
        completeRes.Status.Should().Be(200);

        // E-04 -> E-07: Kiểm tra Header Aggregation
        var inspection = await context.QualityInspections.FirstAsync(x => x.Id == inspectionId);
        inspection.InspectionType.Should().Be(InspectionTypeConstants.Receiving);
        inspection.CompletedAt.Should().NotBeNull();
        inspection.CompletedBy.Should().Be(1001);
        inspection.PassedInspection.Should().BeFalse(); // Có bao ISSUE

        // E-05: Weighted Moisture = (49.8*12.5 + 50.1*14.0 + 49.9*16.0) / 149.8 = 2122.3 / 149.8 = 14.1675...
        decimal expectedWeightedMoisture = (49.8m * 12.5m + 50.1m * 14.0m + 49.9m * 16.0m) / 149.8m;
        inspection.MoisturePercent.Should().Be(expectedWeightedMoisture);

        // E-06: Weighted Impurity = (49.8*1 + 50.1*2 + 49.9*3) / 149.8 = 299.7 / 149.8 = 2.0
        decimal expectedWeightedImpurity = (49.8m * 1.0m + 50.1m * 2.0m + 49.9m * 3.0m) / 149.8m;
        inspection.ImpurityPercent.Should().Be(expectedWeightedImpurity);

        // E-07: AffectedWeightKg = Bag 2 (50.1) + Bag 3 (49.9) = 100.0 kg
        inspection.AffectedWeightKg.Should().Be(100.0m);

        // Side-effects trên Physical Bags:
        var bag1 = await context.PaddyLotBags.FirstAsync(b => b.Id == bagIds[0]);
        var bag2 = await context.PaddyLotBags.FirstAsync(b => b.Id == bagIds[1]);
        var bag3 = await context.PaddyLotBags.FirstAsync(b => b.Id == bagIds[2]);

        bag1.BagKind.Should().Be(PaddyLotBagKinds.Purchase); // ACCEPT_NORMAL giữ nguyên
        bag2.BagKind.Should().Be(PaddyLotBagKinds.Quarantine); // ACCEPT_QUARANTINE đổi sang Quarantine
        bag3.Status.Should().Be(PaddyLotBagStatuses.Reversed); // REJECT_RETURN đổi sang Reversed
        context.PaddyLotBagMovements.Should().Contain(m => m.BagId == bagIds[2] && m.MovementType == PaddyLotBagMovementTypes.QualityRejectReturn);

        // C-03: Dữ liệu bag results vẫn còn nguyên sau khi complete
        var postCompleteProgress = (await sut.GetBagProgressAsync(inspectionId)).Resources as QualityInspectionBagProgressDto;
        postCompleteProgress!.Items.Should().HaveCount(3);

        // E-08: Complete lần 2 -> 409 Conflict
        var completeAgainRes = await sut.CompleteAsync(inspectionId, new CompleteInspectionDto { Note = "Lần 2" });
        completeAgainRes.Status.Should().Be(409);

        // E-09: Thử sửa kết quả bao sau khi Complete -> 400 Bad Request
        var modifyAfterCompleteRes = await sut.SaveBagResultAsync(inspectionId, new SaveBagInspectionResultDto
        {
            BagId = bagIds[0],
            QualityResult = BagQualityResultConstants.Pass,
            Disposition = BagDispositionConstants.AcceptNormal
        });
        modifyAfterCompleteRes.Status.Should().Be(400);
        modifyAfterCompleteRes.Message.Should().Contain("Phiếu kiểm tra đã hoàn thành, không thể chỉnh sửa");
    }
}
