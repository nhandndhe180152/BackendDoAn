using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.RiceVarieties;
using Backend.Application.Implements;
using Backend.Domain.Aggregates;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;
using RiceVarietyEntity = Backend.Domain.Entities.RiceVariety;

namespace Backend.UnitTest.Services.RiceVariety;

/// <summary>
/// Unit tests cho RiceVarietyService - phủ CRUD, trùng mã, không tìm thấy, đã xóa,
/// phân trang và các method NotImplemented.
/// </summary>
public class RiceVarietyServiceTests
{
    private readonly Mock<IRiceVarietyRepository> _repo = new();
    private readonly RiceVarietyService _sut;

    public RiceVarietyServiceTests()
    {
        _sut = new RiceVarietyService(_repo.Object);
    }

    private static CreateRiceVarietyDto ValidCreate() => new()
    {
        Code = "RV001",
        Name = "OM5451",
        Season = "Đông Xuân",
        DefaultYieldRate = 0.65m,
        Note = "Giống chủ lực",
        IsActive = true,
        CreatedBy = 1
    };

    private static UpdateRiceVarietyDto ValidUpdate() => new()
    {
        Id = 1,
        Code = "RV001",
        Name = "OM5451",
        Season = "Đông Xuân",
        DefaultYieldRate = 0.65m,
        IsActive = true,
        UpdatedBy = 1
    };

    private static RiceVarietyEntity Existing(int id = 1, bool isDeleted = false) => new()
    {
        Id = id,
        Code = "RV001",
        Name = "OM5451",
        IsActive = true,
        IsDeleted = isDeleted,
        CreatedDate = DateTime.Now
    };

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<RiceVarietyEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.CreateAsync(It.IsAny<RiceVarietyEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "Create")]
    public async Task Create_Valid_ReturnsCreated_AndPersists()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<RiceVarietyEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(It.IsAny<RiceVarietyEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(201);
        _repo.Verify(r => r.CreateAsync(It.IsAny<RiceVarietyEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "CreateList")]
    public async Task CreateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.CreateListAsync(new[] { ValidCreate() }));
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetAll")]
    public async Task GetAll_ReturnsMappedList()
    {
        var items = new List<RiceVarietyEntity> { Existing(1), Existing(2) };
        _repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<RiceVarietyEntity, bool>>>(), It.IsAny<bool>()))
             .Returns(items.AsQueryable().BuildMock());

        var result = await _sut.GetAllAsync();

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<List<RiceVarietyDetailDto>>();
        ((List<RiceVarietyDetailDto>)result.Resources!).Should().HaveCount(2);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetById")]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((RiceVarietyEntity?)null);

        var result = await _sut.GetByIdAsync(9999);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
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
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetById")]
    public async Task GetById_Found_ReturnsDto()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<RiceVarietyDetailDto>();
        ((RiceVarietyDetailDto)result.Resources!).Id.Should().Be(1);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_DTParameter_ReturnsSuccessWithData()
    {
        var dtResult = new DTResult<RiceVarietyAggregate>
        {
            draw = 1,
            data = new List<RiceVarietyAggregate> { new() { Id = 1, Name = "OM5451", Code = "RV001" } },
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
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_SearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new SearchQuery()));
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_AdvancedSearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new AdvancedSearchQuery<RiceVarietyAggregate>()));
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "Update")]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((RiceVarietyEntity?)null);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<RiceVarietyEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
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
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "Update")]
    public async Task Update_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<RiceVarietyEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<RiceVarietyEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "Update")]
    public async Task Update_Valid_ReturnsSuccess_AndPersists()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<RiceVarietyEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<RiceVarietyEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<RiceVarietyEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.UpdateListAsync(new[] { ValidUpdate() }));
    }

    [Fact]
    [Trait("Service", "RiceVariety")]
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
    [Trait("Service", "RiceVariety")]
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
    [Trait("Service", "RiceVariety")]
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
    [Trait("Service", "RiceVariety")]
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
