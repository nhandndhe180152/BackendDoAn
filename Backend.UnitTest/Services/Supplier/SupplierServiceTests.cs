using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Suppliers;
using Backend.Application.Implements;
using Backend.Domain.Aggregates;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;
using SupplierEntity = Backend.Domain.Entities.Supplier;

namespace Backend.UnitTest.Services.Supplier;

/// <summary>
/// Unit tests cho SupplierService - phủ toàn bộ method và nhánh nghiệp vụ
/// (CRUD, trùng mã, không tìm thấy, đã xóa, phân trang, các method NotImplemented).
/// </summary>
public class SupplierServiceTests
{
    private readonly Mock<ISupplierRepository> _repo = new();
    private readonly SupplierService _sut;

    public SupplierServiceTests()
    {
        _sut = new SupplierService(_repo.Object);
    }

    private static CreateSupplierDto ValidCreate() => new()
    {
        Name = "Công ty TNHH ABC",
        Code = "SUP001",
        ContactPerson = "Nguyễn Văn A",
        Phone = "0901234567",
        Email = "abc@supplier.com",
        Address = "123 Lê Lợi",
        TaxCode = "0312345678",
        IsActive = true,
        CreatedBy = 1
    };

    private static UpdateSupplierDto ValidUpdate() => new()
    {
        Id = 1,
        Name = "Công ty TNHH ABC",
        Code = "SUP001",
        ContactPerson = "Nguyễn Văn A",
        Phone = "0901234567",
        Email = "abc@supplier.com",
        Address = "123 Lê Lợi",
        TaxCode = "0312345678",
        IsActive = true,
        UpdatedBy = 1
    };

    private static SupplierEntity ExistingSupplier(int id = 1, bool isDeleted = false) => new()
    {
        Id = id,
        Name = "Công ty TNHH ABC",
        Code = "SUP001",
        IsActive = true,
        IsDeleted = isDeleted,
        CreatedDate = DateTime.Now
    };

    // ════════════════════════════════════════════════════════════════════════
    // CreateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<SupplierEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.CreateAsync(It.IsAny<SupplierEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Create")]
    public async Task Create_Valid_ReturnsCreated_AndPersists()
    {
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<SupplierEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.CreateAsync(It.IsAny<SupplierEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.CreateAsync(ValidCreate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(201);
        _repo.Verify(r => r.CreateAsync(It.IsAny<SupplierEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Supplier")]
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
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetAll")]
    public async Task GetAll_ReturnsMappedList()
    {
        var suppliers = new List<SupplierEntity>
        {
            ExistingSupplier(1),
            ExistingSupplier(2)
        };
        _repo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<SupplierEntity, bool>>>(), It.IsAny<bool>()))
             .Returns(suppliers.AsQueryable().BuildMock());

        var result = await _sut.GetAllAsync();

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<List<SupplierDetailDto>>();
        ((List<SupplierDetailDto>)result.Resources!).Should().HaveCount(2);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetById")]
    public async Task GetById_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((SupplierEntity?)null);

        var result = await _sut.GetByIdAsync(9999);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetById")]
    public async Task GetById_Deleted_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(ExistingSupplier(1, isDeleted: true));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetById")]
    public async Task GetById_Found_ReturnsDto()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(ExistingSupplier(1));

        var result = await _sut.GetByIdAsync(1);

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        result.Resources.Should().BeOfType<SupplierDetailDto>();
        ((SupplierDetailDto)result.Resources!).Id.Should().Be(1);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetPagedAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_DTParameter_ReturnsSuccessWithData()
    {
        var dtResult = new DTResult<SupplierAggregate>
        {
            draw = 1,
            data = new List<SupplierAggregate> { new() { Id = 1, Name = "ABC", Code = "SUP001" } },
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
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_SearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new SearchQuery()));
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "GetPaged")]
    public async Task GetPaged_AdvancedSearchQuery_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.GetPagedAsync(new AdvancedSearchQuery<SupplierAggregate>()));
    }

    // ════════════════════════════════════════════════════════════════════════
    // UpdateAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Update")]
    public async Task Update_NotFound_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync((SupplierEntity?)null);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<SupplierEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Update")]
    public async Task Update_Deleted_ReturnsNotFound()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(ExistingSupplier(1, isDeleted: true));

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Update")]
    public async Task Update_DuplicateCode_ReturnsUnprocessableEntity()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(ExistingSupplier(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<SupplierEntity, bool>>>()))
             .ReturnsAsync(true);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<SupplierEntity>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "Update")]
    public async Task Update_Valid_ReturnsSuccess_AndPersists()
    {
        _repo.Setup(r => r.GetByIdAsync(It.IsAny<int>()))
             .ReturnsAsync(ExistingSupplier(1));
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<SupplierEntity, bool>>>()))
             .ReturnsAsync(false);
        _repo.Setup(r => r.UpdateAsync(It.IsAny<SupplierEntity>())).Returns(Task.CompletedTask);
        _repo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await _sut.UpdateAsync(ValidUpdate());

        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(200);
        _repo.Verify(r => r.UpdateAsync(It.IsAny<SupplierEntity>()), Times.Once);
        _repo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Supplier")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateList_ThrowsNotImplemented()
    {
        await Assert.ThrowsAsync<NotImplementedException>(
            async () => await _sut.UpdateListAsync(new[] { ValidUpdate() }));
    }

    // ════════════════════════════════════════════════════════════════════════
    // SoftDeleteAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Supplier")]
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
    [Trait("Service", "Supplier")]
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
    [Trait("Service", "Supplier")]
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
    [Trait("Service", "Supplier")]
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
