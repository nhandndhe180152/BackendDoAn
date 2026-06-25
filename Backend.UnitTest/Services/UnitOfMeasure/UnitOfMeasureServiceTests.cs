using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Application.Implements;
using Backend.Domain.Aggregates;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;
using UnitOfMeasureEntity = Backend.Domain.Entities.UnitOfMeasure;

namespace Backend.UnitTest.Services.UnitOfMeasure;

/// <summary>
/// Unit tests cho UnitOfMeasureService - phủ toàn bộ method và nhánh nghiệp vụ.
/// </summary>
public class UnitOfMeasureServiceTests
{
    private readonly Mock<IUnitOfMeasureRepository> _repo = new();
    private readonly UnitOfMeasureService _sut;

    public UnitOfMeasureServiceTests()
    {
        _sut = new UnitOfMeasureService(_repo.Object);
    }

    private static CreateUnitOfMeasureDto ValidCreate() => new()
    {
        Name = "Kilôgram",
        Symbol = "kg",
        CreatedBy = 1
    };

    private static UpdateUnitOfMeasureDto ValidUpdate() => new()
    {
        Id = 1,
        Name = "Kilôgram",
        Symbol = "kg",
        UpdatedBy = 1
    };

    private static UnitOfMeasureEntity Existing(int id = 1, bool isDeleted = false) => new()
    {
        Id = id,
        Name = "Kilôgram",
        Symbol = "kg",
        IsDeleted = isDeleted,
        CreatedDate = DateTime.Now
    };

    // ════════════════════════════════════════════════════════════════════════
    // CreateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Create")]
    public async Task Create_Duplicate_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<UnitOfMeasureEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.CreateAsync(It.IsAny<UnitOfMeasureEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Create")]
    public async Task Create_Valid_ReturnsCreated_AndPersists()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<UnitOfMeasureEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(It.IsAny<UnitOfMeasureEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(201);
        _repo.Verify(r => r.CreateAsync(It.IsAny<UnitOfMeasureEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "CreateList")]
    public async Task CreateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.CreateListAsync(new[] { ValidCreate() }));
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetAllAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetAll")]
    public async Task GetAll_ReturnsMappedList()
    {
        var units = new List<UnitOfMeasureEntity> { Existing(1), Existing(2) };
        _repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<UnitOfMeasureEntity, bool>>>(), It.IsAny<bool>()))
             .Returns(units.AsQueryable().BuildMock());

        var result = await _sut.GetAllAsync();

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<List<UnitOfMeasureDetailDto>>();
        ((List<UnitOfMeasureDetailDto>)result.Resources!).Should().HaveCount(2);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetById")]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((UnitOfMeasureEntity?)null);

        var result = await _sut.GetByIdAsync(9999);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetById")]
    public async Task GetById_Deleted_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1, isDeleted: true));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetById")]
    public async Task GetById_Found_ReturnsDto()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<UnitOfMeasureDetailDto>();
        ((UnitOfMeasureDetailDto)result.Resources!).Id.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetPagedAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_DTParameter_ReturnsSuccessWithData()
    {
        var dtResult = new DTResult<UnitOfMeasureAggregate>
        {
            draw = 1,
            data = new List<UnitOfMeasureAggregate> { new() { Id = 1, Name = "Kilôgram", Symbol = "kg" } },
            recordsFiltered = 1,
            recordsTotal = 1
        };
        _repo.Setup(r => r.GetPagedAsync(It.IsAny<DTParameter>()))
             .ReturnsAsync(dtResult);

        var result = await _sut.GetPagedAsync(new DTParameter());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().Be(dtResult);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_SearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new SearchQuery()));
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_AdvancedSearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new AdvancedSearchQuery<UnitOfMeasureAggregate>()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // UpdateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Update")]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((UnitOfMeasureEntity?)null);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UnitOfMeasureEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Update")]
    public async Task Update_Deleted_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1, isDeleted: true));

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Update")]
    public async Task Update_Duplicate_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<UnitOfMeasureEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UnitOfMeasureEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "Update")]
    public async Task Update_Valid_ReturnsSuccess_AndPersists()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<UnitOfMeasureEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<UnitOfMeasureEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<UnitOfMeasureEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.UpdateListAsync(new[] { ValidUpdate() }));
    }

    // ════════════════════════════════════════════════════════════════════════
    // SoftDeleteAsync / SoftDeleteListAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_Fails_ReturnsBadRequest()
    {
        _repo.Setup(r => r.SoftDeleteAsync(It.IsAny<int>())).ReturnsAsync(false);

        var result = await _sut.SoftDeleteAsync(1);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_Success_ReturnsSuccess()
    {
        _repo.Setup(r => r.SoftDeleteAsync(It.IsAny<int>())).ReturnsAsync(true);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.SoftDeleteAsync(1);

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteList_Fails_ReturnsBadRequest()
    {
        _repo.Setup(r => r.SoftDeleteListAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(false);

        var result = await _sut.SoftDeleteListAsync(new[] { 1, 2 });

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "UnitOfMeasure")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteList_Success_ReturnsSuccess()
    {
        _repo.Setup(r => r.SoftDeleteListAsync(It.IsAny<IEnumerable<int>>())).ReturnsAsync(true);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.SoftDeleteListAsync(new[] { 1, 2 });

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }
}
