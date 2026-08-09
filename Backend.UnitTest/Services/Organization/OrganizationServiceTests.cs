using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Organizations;
using Backend.Application.Implements;
using Backend.Domain.Aggregates;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;
using OrganizationEntity = Backend.Domain.Entities.Organization;

namespace Backend.UnitTest.Services.Organization;

/// <summary>
/// Unit tests cho OrganizationService - phủ CRUD, trùng mã, không tìm thấy, đã xóa,
/// phân trang và các method NotImplemented.
/// </summary>
public class OrganizationServiceTests
{
    private readonly Mock<IOrganizationRepository> _repo = new();
    private readonly OrganizationService _sut;

    public OrganizationServiceTests()
    {
        _sut = new OrganizationService(_repo.Object);
    }

    private static CreateOrganizationDto ValidCreate() => new()
    {
        Code = "ORG001",
        Name = "Doanh nghiệp Tuấn Mây",
        Description = "Chuỗi cung ứng lúa gạo",
        TaxCode = "0312111222",
        Address = "12 Nguyễn Huệ",
        ContactEmail = "tuanmay@gmail.com",
        ContactPhone = "0900112233",
        IsActive = true,
        CreatedBy = 1
    };

    private static UpdateOrganizationDto ValidUpdate() => new()
    {
        Id = 1,
        Code = "ORG001",
        Name = "Doanh nghiệp Tuấn Mây",
        TaxCode = "0312111222",
        IsActive = true,
        UpdatedBy = 1
    };

    private static OrganizationEntity Existing(int id = 1, bool isDeleted = false) => new()
    {
        Id = id,
        Code = "ORG001",
        Name = "Doanh nghiệp Tuấn Mây",
        IsActive = true,
        IsDeleted = isDeleted,
        CreatedDate = DateTime.Now
    };

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.CreateAsync(It.IsAny<OrganizationEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicatePhone_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync((Expression<Func<OrganizationEntity, bool>> expr) =>
             {
                 var str = expr.ToString();
                 if (str.Contains("Code")) return false;
                 if (str.Contains("ContactPhone")) return true;
                 return false;
             });

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.CreateAsync(It.IsAny<OrganizationEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Create")]
    public async Task Create_Valid_ReturnsCreated_AndPersists()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(It.IsAny<OrganizationEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(201);
        _repo.Verify(r => r.CreateAsync(It.IsAny<OrganizationEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "CreateList")]
    public async Task CreateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.CreateListAsync(new[] { ValidCreate() }));
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "GetAll")]
    public async Task GetAll_ReturnsMappedList()
    {
        var items = new List<OrganizationEntity> { Existing(1), Existing(2) };
        _repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<OrganizationEntity, bool>>>(), It.IsAny<bool>()))
             .Returns(items.AsQueryable().BuildMock());

        var result = await _sut.GetAllAsync();

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<List<OrganizationDetailDto>>();
        ((List<OrganizationDetailDto>)result.Resources!).Should().HaveCount(2);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "GetById")]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((OrganizationEntity?)null);

        var result = await _sut.GetByIdAsync(9999);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Organization")]
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
    [Trait("Service", "Organization")]
    [Trait("Method", "GetById")]
    public async Task GetById_Found_ReturnsDto()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<OrganizationDetailDto>();
        ((OrganizationDetailDto)result.Resources!).Id.Should().Be(1);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_DTParameter_ReturnsSuccessWithData()
    {
        var dtResult = new DTResult<OrganizationAggregate>
        {
            draw = 1,
            data = new List<OrganizationAggregate> { new() { Id = 1, Name = "Tuấn Mây", Code = "ORG001" } },
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
    [Trait("Service", "Organization")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_SearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new SearchQuery()));
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_AdvancedSearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new AdvancedSearchQuery<OrganizationAggregate>()));
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Update")]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((OrganizationEntity?)null);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Organization")]
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
    [Trait("Service", "Organization")]
    [Trait("Method", "Update")]
    public async Task Update_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Update")]
    public async Task Update_DuplicatePhone_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync((Expression<Func<OrganizationEntity, bool>> expr) =>
             {
                 var str = expr.ToString();
                 if (str.Contains("Code")) return false;
                 if (str.Contains("ContactPhone")) return true;
                 return false;
             });

        var dto = ValidUpdate();
        dto.ContactPhone = "0900112233";
        var result = await _sut.UpdateAsync(dto);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "Update")]
    public async Task Update_Valid_ReturnsSuccess_AndPersists()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(Existing(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<OrganizationEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<OrganizationEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<OrganizationEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Organization")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.UpdateListAsync(new[] { ValidUpdate() }));
    }

    [Fact]
    [Trait("Service", "Organization")]
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
    [Trait("Service", "Organization")]
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
    [Trait("Service", "Organization")]
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
    [Trait("Service", "Organization")]
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
