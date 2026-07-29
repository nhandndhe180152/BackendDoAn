using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.QualityInspections;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

using PaddyLotEntity = Backend.Domain.Entities.PaddyLot;
using InventoryEntity = Backend.Domain.Entities.Inventory;
using Backend.Domain.Abstractions.Repositories;

namespace Backend.UnitTest.Services.QualityInspection;

/// <summary>
/// Unit tests cho QualityInspectionService.
/// Kiểm tra: #12 (transaction atomicity) và #14 (QualityStatus sync).
/// </summary>
[Trait("Service", "QualityInspection")]
public class QualityInspectionServiceTests
{
    private readonly Mock<IQualityInspectionRepository> _repo = new();
    private readonly Mock<IPaddyLotRepository> _lotRepo = new();
    private readonly Mock<IInventoryRepository> _inventoryRepo = new();
    private readonly Mock<IInventoryTransactionRepository> _inventoryTxRepo = new();
    private readonly Mock<IRepositoryBase<LotStatus, int>> _lotStatusRepo = new();
    private readonly Mock<IApplicationDbContext> _context = new();
    private readonly Mock<INotificationDispatcher> _notificationDispatcher = new();
    private readonly List<PaddyLotEntity> _paddyLots = new();

    public QualityInspectionServiceTests()
    {
        _context.Setup(c => c.PaddyLots).Returns(() => MockDbSet(_paddyLots).Object);
        _inventoryRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<InventoryEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(new List<InventoryEntity>().AsQueryable().BuildMock());
    }

    private QualityInspectionService Sut() =>
        new(_repo.Object, _lotRepo.Object, _inventoryRepo.Object, _inventoryTxRepo.Object, _lotStatusRepo.Object, _context.Object, _notificationDispatcher.Object);

    // ── Helper: setup mock DB transaction ────────────────────────────────────

    private static Mock<IDbContextTransaction> SetupTransaction(Mock<IQualityInspectionRepository> repo)
    {
        var tx = new Mock<IDbContextTransaction>();
        tx.Setup(t => t.CommitAsync(default)).Returns(Task.CompletedTask);
        tx.Setup(t => t.RollbackAsync(default)).Returns(Task.CompletedTask);
        tx.Setup(t => t.DisposeAsync()).Returns(ValueTask.CompletedTask);
        repo.Setup(r => r.BeginTransactionAsync()).ReturnsAsync(tx.Object);
        return tx;
    }

    // ── CreateAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateAsync_LotNotFound_Returns404_WithoutTransaction()
    {
        // Arrange
        _lotRepo.Setup(r => r.GetByIdAsync(99)).ReturnsAsync((PaddyLotEntity?)null);

        // Act
        var result = await Sut().CreateAsync(new CreateQualityInspectionDto { PaddyLotId = 99 });

        // Assert
        result.Status.Should().Be(404);
        _repo.Verify(r => r.BeginTransactionAsync(), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_PassedInspection_SetsQualityStatus_PASSED_AndCommits()
    {
        // Arrange
        var lot = new PaddyLotEntity { Id = 1, IsDeleted = false, QualityStatus = null };
        _lotRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(lot);
        SetupTransaction(_repo);
        _repo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
             .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.UpdateAsync(lot)).Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 1,
            PassedInspection = true,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        lot.QualityStatus.Should().Be("PASSED");
        _lotRepo.Verify(r => r.UpdateAsync(lot), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_FailedInspection_SetsQualityStatus_FAILED_AndCommits()
    {
        // Arrange
        var lot = new PaddyLotEntity { Id = 2, IsDeleted = false, QualityStatus = "PASSED" };
        _lotRepo.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(lot);
        SetupTransaction(_repo);
        _repo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
             .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.UpdateAsync(lot)).Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 2,
            PassedInspection = false,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        lot.QualityStatus.Should().Be("FAILED");
    }

    [Fact]
    public async Task CreateAsync_WhenRepoThrows_TransactionRollsBack()
    {
        // Arrange
        var lot = new PaddyLotEntity { Id = 3, IsDeleted = false };
        _lotRepo.Setup(r => r.GetByIdAsync(3)).ReturnsAsync(lot);
        var tx = SetupTransaction(_repo);
        _repo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
             .ThrowsAsync(new Exception("DB error"));

        // Act + Assert
        await Assert.ThrowsAsync<Exception>(() =>
            Sut().CreateAsync(new CreateQualityInspectionDto
            {
                PaddyLotId = 3,
                InspectedAt = DateTime.UtcNow
            }));

        tx.Verify(t => t.RollbackAsync(default), Times.Once);
        tx.Verify(t => t.CommitAsync(default), Times.Never);
    }

    // ── UpdateAsync (#14 — sync QualityStatus) ───────────────────────────────

    [Fact]
    public async Task UpdateAsync_PassedTrue_SyncsQualityStatus_PASSED_ToLot()
    {
        // Arrange
        var inspection = new Domain.Entities.QualityInspection { Id = 10, PaddyLotId = 5, IsDeleted = false };
        var lot = new PaddyLotEntity { Id = 5, IsDeleted = false, QualityStatus = "FAILED" };

        _repo.Setup(r => r.GetByIdAsync(10)).ReturnsAsync(inspection);
        _repo.Setup(r => r.UpdateAsync(inspection)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(lot);
        _lotRepo.Setup(r => r.UpdateAsync(lot)).Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        SetupTransaction(_repo);

        var dto = new UpdateQualityInspectionDto
        {
            Id = 10, PaddyLotId = 5, PassedInspection = true, InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().UpdateAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        lot.QualityStatus.Should().Be("PASSED");
        _lotRepo.Verify(r => r.UpdateAsync(lot), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_PassedFalse_SyncsQualityStatus_FAILED_ToLot()
    {
        var inspection = new Domain.Entities.QualityInspection { Id = 11, PaddyLotId = 6, IsDeleted = false };
        var lot = new PaddyLotEntity { Id = 6, IsDeleted = false, QualityStatus = "PASSED" };

        _repo.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(inspection);
        _repo.Setup(r => r.UpdateAsync(inspection)).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.GetByIdAsync(6)).ReturnsAsync(lot);
        _lotRepo.Setup(r => r.UpdateAsync(lot)).Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        SetupTransaction(_repo);

        var dto = new UpdateQualityInspectionDto
        {
            Id = 11, PaddyLotId = 6, PassedInspection = false, InspectedAt = DateTime.UtcNow
        };

        var result = await Sut().UpdateAsync(dto);

        result.Status.Should().Be(200);
        lot.QualityStatus.Should().Be("FAILED");
    }

    [Fact]
    public async Task UpdateAsync_InspectionNotFound_Returns404()
    {
        _repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.QualityInspection?)null);

        var result = await Sut().UpdateAsync(new UpdateQualityInspectionDto { Id = 999 });

        result.Status.Should().Be(404);
        _lotRepo.Verify(r => r.UpdateAsync(It.IsAny<PaddyLotEntity>()), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_FailedInspectionWithPartialAffectedWeight_SplitsLotAndAdjustsInventory()
    {
        // Arrange
        var existing = new Domain.Entities.QualityInspection
        {
            Id = 11,
            PaddyLotId = 5,
            PassedInspection = true, // was not split
            AffectedWeightKg = null,
            IsDeleted = false
        };
        _repo.Setup(r => r.GetByIdAsync(11)).ReturnsAsync(existing);

        var parentLot = new PaddyLotEntity
        {
            Id = 5,
            LotCode = "LOT-5",
            InitialWeightKg = 10000,
            RemainingWeightKg = 10000,
            LotType = "PADDY",
            ProductVariantId = 1,
            WarehouseId = 2,
            LocationId = 3,
            InboundDate = DateTime.UtcNow,
            CostPricePerKg = 12000,
            StatusId = 1
        };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        var quarantineStatus = new LotStatus { Id = 2, Code = "QUARANTINE" };
        var inStockStatus = new LotStatus { Id = 3, Code = "IN_STOCK" };

        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<LotStatus, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<LotStatus, bool>> predicate, bool trackChanges, Expression<Func<LotStatus, object>>[] includes) => {
                var str = predicate.ToString();
                if (str.Contains("QUARANTINE")) return quarantineStatus;
                if (str.Contains("IN_STOCK")) return inStockStatus;
                return null;
            });

        var parentInv = new InventoryEntity
        {
            Id = 10,
            PaddyLotId = 5,
            QuantityOnHand = 10000,
            QuantityReserved = 0,
            CostPrice = 12000,
            WarehouseId = 2,
            LocationId = 3,
            ProductVariantId = 1
        };
        var inventories = new List<InventoryEntity> { parentInv };

        _inventoryRepo.Setup(r => r.FindByCondition(
            It.IsAny<Expression<Func<InventoryEntity, bool>>>(),
            It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        _repo.Setup(r => r.UpdateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.CreateAsync(It.IsAny<PaddyLotEntity>()))
            .Callback<PaddyLotEntity>(child => child.Id = 6) // assign ID
            .Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.UpdateAsync(parentLot)).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.UpdateAsync(parentInv)).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.CreateAsync(It.IsAny<InventoryEntity>())).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryTxRepo.Setup(r => r.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTxRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new UpdateQualityInspectionDto
        {
            Id = 11,
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = 3000,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().UpdateAsync(dto);

        // Assert
        result.Status.Should().Be(200);

        // Parent lot checks
        parentLot.RemainingWeightKg.Should().Be(7000);
        parentLot.InitialWeightKg.Should().Be(10000);
        parentLot.QualityStatus.Should().Be("PASSED");
        parentLot.StatusId.Should().Be(3); // IN_STOCK

        // Parent inventory checks
        parentInv.QuantityOnHand.Should().Be(7000);

        // Verify child lot created
        _lotRepo.Verify(r => r.CreateAsync(It.Is<PaddyLotEntity>(c =>
            c.LotCode == "LOT-5-Q1" &&
            c.ParentLotId == 5 &&
            c.InitialWeightKg == 3000 &&
            c.RemainingWeightKg == 3000 &&
            c.StatusId == 2 && // QUARANTINE
            c.QualityStatus == QualityStatusConstants.Failed
        )), Times.Once);

        // Verify child inventory created
        _inventoryRepo.Verify(r => r.CreateAsync(It.Is<InventoryEntity>(i =>
            i.PaddyLotId == 6 &&
            i.QuantityOnHand == 3000
        )), Times.Once);

        // Verify transactions created with MANUAL_ADJUST
        _inventoryTxRepo.Verify(r => r.CreateAsync(It.Is<InventoryTransaction>(t =>
            t.PaddyLotId == 5 &&
            t.TransactionType == InventoryTransactionTypeConstants.ManualAdjust &&
            t.Quantity == 3000 &&
            t.ReferenceType == InventoryReferenceTypeConstants.QualityInspectionSplit
        )), Times.Once);
        _inventoryTxRepo.Verify(r => r.CreateAsync(It.Is<InventoryTransaction>(t =>
            t.PaddyLotId == 6 &&
            t.TransactionType == InventoryTransactionTypeConstants.ManualAdjust &&
            t.Quantity == 3000 &&
            t.ReferenceType == InventoryReferenceTypeConstants.QualityInspectionSplit
        )), Times.Once);
    }

    // ── GetByIdAsync ─────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _repo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.QualityInspection, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Domain.Entities.QualityInspection, object>>[]>()))
             .Returns(new List<Domain.Entities.QualityInspection>().AsQueryable().BuildMock());

        var result = await Sut().GetByIdAsync(999);

        result.Status.Should().Be(404);
    }

    // ── Bulk stubs → 501 ─────────────────────────────────────────────────────

    [Fact]
    public async Task CreateListAsync_Returns501()
    {
        var result = await Sut().CreateListAsync(new List<CreateQualityInspectionDto>());
        result.Status.Should().Be(501);
    }

    [Fact]
    public async Task UpdateListAsync_Returns501()
    {
        var result = await Sut().UpdateListAsync(new List<UpdateQualityInspectionDto>());
        result.Status.Should().Be(501);
    }

    // ── SoftDeleteAsync ──────────────────────────────────────────────────────

    [Fact]
    public async Task SoftDeleteAsync_Returns404_WhenNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Domain.Entities.QualityInspection?)null);

        var result = await Sut().SoftDeleteAsync(999);

        result.Status.Should().Be(404);
    }



    [Fact]
    public async Task CreateAsync_FailedInspectionWithPartialAffectedWeight_SplitsLotAndAdjustsInventory()
    {
        // Arrange
        var parentLot = new PaddyLotEntity 
        { 
            Id = 5, 
            LotCode = "LOT-5", 
            InitialWeightKg = 10000, 
            RemainingWeightKg = 10000, 
            LotType = "PADDY", 
            ProductVariantId = 1,
            WarehouseId = 2,
            LocationId = 3,
            InboundDate = DateTime.UtcNow,
            CostPricePerKg = 12000,
            StatusId = 1
        };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        var quarantineStatus = new LotStatus { Id = 2, Code = "QUARANTINE" };
        var inStockStatus = new LotStatus { Id = 3, Code = "IN_STOCK" };

        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(
            It.IsAny<Expression<Func<LotStatus, bool>>>(),
            It.IsAny<bool>(),
            It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<LotStatus, bool>> predicate, bool trackChanges, Expression<Func<LotStatus, object>>[] includes) => {
                var str = predicate.ToString();
                if (str.Contains("QUARANTINE")) return quarantineStatus;
                if (str.Contains("IN_STOCK")) return inStockStatus;
                return null;
            });

        var parentInv = new InventoryEntity 
        { 
            Id = 10, 
            PaddyLotId = 5, 
            QuantityOnHand = 10000, 
            QuantityReserved = 0,
            CostPrice = 12000,
            WarehouseId = 2,
            LocationId = 3,
            ProductVariantId = 1
        };
        var inventories = new List<InventoryEntity> { parentInv };
        
        _inventoryRepo.Setup(r => r.FindByCondition(
            It.IsAny<Expression<Func<InventoryEntity, bool>>>(),
            It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        _repo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
            .Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.CreateAsync(It.IsAny<PaddyLotEntity>()))
            .Callback<PaddyLotEntity>(child => child.Id = 6) // assign ID
            .Returns(Task.CompletedTask);
        _lotRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _lotRepo.Setup(r => r.UpdateAsync(parentLot)).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.UpdateAsync(parentInv)).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.CreateAsync(It.IsAny<InventoryEntity>())).Returns(Task.CompletedTask);
        _inventoryRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryTxRepo.Setup(r => r.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTxRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = 3000,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        
        // Parent lot checks (B4: InitialWeightKg must not change)
        parentLot.RemainingWeightKg.Should().Be(7000);
        parentLot.InitialWeightKg.Should().Be(10000);
        parentLot.QualityStatus.Should().Be("PASSED");
        parentLot.StatusId.Should().Be(3); // IN_STOCK

        // Parent inventory checks
        parentInv.QuantityOnHand.Should().Be(7000);

        // Verify child lot created (B9: Copy SourceReceiptId and SourceMillingOrderId)
        _lotRepo.Verify(r => r.CreateAsync(It.Is<PaddyLotEntity>(c => 
            c.LotCode == "LOT-5-Q1" &&
            c.ParentLotId == 5 &&
            c.InitialWeightKg == 3000 &&
            c.RemainingWeightKg == 3000 &&
            c.StatusId == 2 && // QUARANTINE
            c.QualityStatus == QualityStatusConstants.Failed
        )), Times.Once);

        // Verify child inventory created
        _inventoryRepo.Verify(r => r.CreateAsync(It.Is<InventoryEntity>(i => 
            i.PaddyLotId == 6 &&
            i.QuantityOnHand == 3000
        )), Times.Once);

        // Verify transactions created with MANUAL_ADJUST (B5)
        _inventoryTxRepo.Verify(r => r.CreateAsync(It.Is<InventoryTransaction>(t => 
            t.PaddyLotId == 5 && 
            t.TransactionType == InventoryTransactionTypeConstants.ManualAdjust && 
            t.Quantity == 3000 &&
            t.ReferenceType == InventoryReferenceTypeConstants.QualityInspectionSplit
        )), Times.Once);
        _inventoryTxRepo.Verify(r => r.CreateAsync(It.Is<InventoryTransaction>(t => 
            t.PaddyLotId == 6 && 
            t.TransactionType == InventoryTransactionTypeConstants.ManualAdjust && 
            t.Quantity == 3000 &&
            t.ReferenceType == InventoryReferenceTypeConstants.QualityInspectionSplit
        )), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_SplitLot_ThrowsBadRequest_WhenAvailableInventoryInsufficient()
    {
        // Arrange (B1: Available stock < AffectedWeightKg)
        var parentLot = new PaddyLotEntity { Id = 5, RemainingWeightKg = 10000 };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        var parentInv = new InventoryEntity { PaddyLotId = 5, QuantityOnHand = 10000, QuantityReserved = 9500 }; // Only 500 available
        var inventories = new List<InventoryEntity> { parentInv };
        _inventoryRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<InventoryEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = 3000, // Requires 3000
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("Không đủ tồn kho khả dụng");
    }

    [Fact]
    public async Task CreateAsync_SplitLot_IgnoresReservedQuantitiesDuringDeduction()
    {
        // Arrange (B2: Deduct only from available quantity)
        var parentLot = new PaddyLotEntity 
        { 
            Id = 5, LotCode = "LOT-5", RemainingWeightKg = 10000, InitialWeightKg = 10000 
        };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        var quarantineStatus = new LotStatus { Id = 2, Code = "QUARANTINE" };
        var inStockStatus = new LotStatus { Id = 3, Code = "IN_STOCK" };
        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LotStatus, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<LotStatus, bool>> pred, bool tc, Expression<Func<LotStatus, object>>[] inc) => 
                pred.ToString().Contains("QUARANTINE") ? quarantineStatus : inStockStatus);

        var parentInv = new InventoryEntity 
        { 
            Id = 10, PaddyLotId = 5, QuantityOnHand = 10000, QuantityReserved = 4000 
        }; // Available = 6000
        var inventories = new List<InventoryEntity> { parentInv };
        _inventoryRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<InventoryEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = 3000,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        parentInv.QuantityOnHand.Should().Be(7000); // 10000 - 3000
        parentInv.QuantityReserved.Should().Be(4000); // Untouched
    }

    [Fact]
    public async Task CreateAsync_SplitLot_AvoidsDuplicateLotCodeFromSoftDeleted()
    {
        // Arrange (B3: Soft-deleted LOT-5-Q1 exists, should name child lot LOT-5-Q2)
        var parentLot = new PaddyLotEntity { Id = 5, LotCode = "LOT-5", RemainingWeightKg = 10000 };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        // Populate context with soft-deleted lot
        var deletedLot = new PaddyLotEntity { LotCode = "LOT-5-Q1", OrganizationId = parentLot.OrganizationId, IsDeleted = true };
        _paddyLots.Add(deletedLot);

        var quarantineStatus = new LotStatus { Id = 2, Code = "QUARANTINE" };
        var inStockStatus = new LotStatus { Id = 3, Code = "IN_STOCK" };
        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LotStatus, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync((Expression<Func<LotStatus, bool>> pred, bool tc, Expression<Func<LotStatus, object>>[] inc) => 
                pred.ToString().Contains("QUARANTINE") ? quarantineStatus : inStockStatus);

        var parentInv = new InventoryEntity { Id = 10, PaddyLotId = 5, QuantityOnHand = 10000 };
        var inventories = new List<InventoryEntity> { parentInv };
        _inventoryRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<InventoryEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = 3000,
            InspectedAt = DateTime.UtcNow
        };

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        _lotRepo.Verify(r => r.CreateAsync(It.Is<PaddyLotEntity>(c => c.LotCode == "LOT-5-Q2")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WholeLotFailed_WritesQuarantineTransactions()
    {
        // Arrange (B10: All-or-nothing failure logs quarantine transaction)
        var parentLot = new PaddyLotEntity { Id = 5, LotCode = "LOT-5", RemainingWeightKg = 10000 };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(parentLot);
        SetupTransaction(_repo);

        var quarantineStatus = new LotStatus { Id = 2, Code = "QUARANTINE" };
        _lotStatusRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<LotStatus, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<LotStatus, object>>[]>()))
            .ReturnsAsync(quarantineStatus);

        var parentInv = new InventoryEntity { Id = 10, PaddyLotId = 5, QuantityOnHand = 10000 };
        var inventories = new List<InventoryEntity> { parentInv };
        _inventoryRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<InventoryEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        var dto = new CreateQualityInspectionDto
        {
            PaddyLotId = 5,
            PassedInspection = false,
            AffectedWeightKg = null, // All-or-nothing
            InspectedAt = DateTime.UtcNow
        };

        _repo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.QualityInspection>()))
             .Callback<Domain.Entities.QualityInspection>(e => e.Id = 99)
             .Returns(Task.CompletedTask);

        // Act
        var result = await Sut().CreateAsync(dto);

        // Assert
        result.Status.Should().BeOneOf(200, 201);
        _inventoryTxRepo.Verify(r => r.CreateAsync(It.Is<InventoryTransaction>(t => 
            t.PaddyLotId == 5 && 
            t.TransactionType == InventoryTransactionTypeConstants.ManualAdjust && 
            t.ReferenceType == InventoryReferenceTypeConstants.QualityInspectionQuarantine &&
            t.Quantity == 0 &&
            t.ReferenceId == 99
        )), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_SplitInspection_ThrowsBadRequest_WhenCriticalFieldsChanged()
    {
        // Arrange (B7: Block editing split inspection critical fields)
        var existing = new Domain.Entities.QualityInspection 
        { 
            Id = 1, PaddyLotId = 5, PassedInspection = false, AffectedWeightKg = 3000 
        };
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        var lot = new PaddyLotEntity { Id = 5, IsDeleted = false, RemainingWeightKg = 5000 };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(lot);

        var dto = new UpdateQualityInspectionDto
        {
            Id = 1,
            PaddyLotId = 5,
            PassedInspection = true, // Critical change
            AffectedWeightKg = 3000
        };

        // Act
        var result = await Sut().UpdateAsync(dto);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("Không thể thay đổi Lô lúa/gạo, Kết quả kiểm định hoặc Khối lượng ảnh hưởng");
    }

    [Fact]
    public async Task UpdateAsync_SplitInspection_AllowsEditingMinorFields_AndDoesNotChangeLotStatus()
    {
        // Arrange (C2: Cho phép sửa Note, không lật QualityStatus của lô gốc)
        var existing = new Domain.Entities.QualityInspection 
        { 
            Id = 1, PaddyLotId = 5, PassedInspection = false, AffectedWeightKg = 3000, Note = "Old" 
        };
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        var lot = new PaddyLotEntity { Id = 5, IsDeleted = false, RemainingWeightKg = 5000 };
        _lotRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync(lot);
        SetupTransaction(_repo);

        var dto = new UpdateQualityInspectionDto
        {
            Id = 1, PaddyLotId = 5, PassedInspection = false, AffectedWeightKg = 3000, Note = "New"
        };

        // Act
        var result = await Sut().UpdateAsync(dto);

        // Assert
        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(It.Is<Domain.Entities.QualityInspection>(q => q.Note == "New")), Times.Once);
        // C2: Đảm bảo lô gốc KHÔNG bị cập nhật trạng thái
        _lotRepo.Verify(r => r.UpdateAsync(It.IsAny<PaddyLotEntity>()), Times.Never);
    }

    [Fact]
    public async Task SoftDeleteAsync_SplitInspection_ThrowsBadRequest()
    {
        // Arrange (B7: Block deleting split inspection)
        var existing = new Domain.Entities.QualityInspection 
        { 
            Id = 1, PaddyLotId = 5, PassedInspection = false, AffectedWeightKg = 3000 
        };
        _repo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(existing);

        // Act
        var result = await Sut().SoftDeleteAsync(1);

        // Assert
        result.Status.Should().Be(400);
        result.Message.Should().Contain("Không thể xóa phiếu kiểm định đã thực hiện tách lô");
    }

    private static Mock<DbSet<T>> MockDbSet<T>(List<T> list) where T : class
    {
        var mockQueryable = list.AsQueryable().BuildMock();
        var mockDbSet = new Mock<DbSet<T>>();

        mockDbSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(mockQueryable.Provider);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(mockQueryable.Expression);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(mockQueryable.ElementType);
        mockDbSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(() => mockQueryable.GetEnumerator());

        mockDbSet.As<IAsyncEnumerable<T>>()
            .Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>()))
            .Returns(((IAsyncEnumerable<T>)mockQueryable).GetAsyncEnumerator(default));

        return mockDbSet;
    }
}
