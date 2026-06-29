using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Warehouses;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Warehouse;

public class WarehouseServiceTests
{
    private readonly Mock<IWarehouseRepository> _warehouseRepository = new();
    private readonly WarehouseService _sut;

    public WarehouseServiceTests()
    {
        _sut = new WarehouseService(_warehouseRepository.Object);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenCodeDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateWarehouseDto { Code = "WH001", Name = "Warehouse 1" };

        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        _warehouseRepository.Verify(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Warehouse>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new CreateWarehouseDto { Code = "WH001", Name = "Warehouse 1" };

        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(false);

        _warehouseRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Warehouse>()))
            .Returns(Task.CompletedTask);

        _warehouseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        _warehouseRepository.Verify(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Warehouse>()), Times.Once);
        _warehouseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_CreatesAllAndSaves()
    {
        // Arrange
        var dtos = new List<CreateWarehouseDto>
        {
            new() { Code = "WH1", Name = "Warehouse 1" },
            new() { Code = "WH2", Name = "Warehouse 2" }
        };

        _warehouseRepository
            .Setup(repo => repo.CreateListAsync(It.IsAny<IEnumerable<Backend.Domain.Entities.Warehouse>>()))
            .Returns(Task.CompletedTask);

        _warehouseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(2);

        // Act
        var response = await _sut.CreateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        _warehouseRepository.Verify(repo => repo.CreateListAsync(It.IsAny<IEnumerable<Backend.Domain.Entities.Warehouse>>()), Times.Once);
        _warehouseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_ReturnsActiveWarehouses()
    {
        // Arrange
        var warehouses = new List<Backend.Domain.Entities.Warehouse>
        {
            new() { Id = 1, Code = "WH1", Name = "WH 1", IsDeleted = false },
            new() { Id = 2, Code = "WH2", Name = "WH 2", IsDeleted = false }
        };

        SetupWarehouses(warehouses);

        // Act
        var response = await _sut.GetAllAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var data = response.Resources as List<WarehouseDetailDto>;
        data.Should().NotBeNull();
        data.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _warehouseRepository
            .Setup(repo => repo.GetByIdAsync(99))
            .ReturnsAsync((Backend.Domain.Entities.Warehouse?)null);

        // Act
        var response = await _sut.GetByIdAsync(99);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenExists_ReturnsWarehouseDetail()
    {
        // Arrange
        var warehouse = new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH1", Name = "Warehouse 1" };
        _warehouseRepository
            .Setup(repo => repo.GetByIdAsync(1))
            .ReturnsAsync(warehouse);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var detail = response.Resources as WarehouseDetailDto;
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(1);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_FiltersByKeyword()
    {
        // Arrange
        var list = new List<Backend.Domain.Entities.Warehouse>
        {
            new() { Id = 1, Code = "WH1", Name = "Hanoi Warehouse", Address = "Hanoi", IsDeleted = false },
            new() { Id = 2, Code = "WH2", Name = "HCM Warehouse", Address = "HCM", IsDeleted = false }
        };
        SetupWarehouses(list);

        var query = new SearchQuery { PageIndex = 1, PageSize = 10, Keyword = "HCM" };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var pagedData = response.Resources as PagingData<WarehouseListDto>;
        pagedData.Should().NotBeNull();
        pagedData!.TotalFiltered.Should().Be(1);
        pagedData.DataSource.First().Name.Should().Be("HCM Warehouse");
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsResult()
    {
        // Arrange
        var parameters = new DTParameter();
        var mockResult = new DTResult<WarehouseAggregate> { draw = 1 };

        _warehouseRepository
            .Setup(repo => repo.GetPagedAsync(parameters))
            .ReturnsAsync(mockResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(mockResult);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenNotExists_ReturnsBadRequest()
    {
        // Arrange
        _warehouseRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenExists_SavesAndReturnsSuccess()
    {
        // Arrange
        _warehouseRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(true);
        _warehouseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _warehouseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDuplicateCode_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new UpdateWarehouseDto { Id = 1, Code = "WH-DUP" };
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateWarehouseDto { Id = 1, Code = "WH-OK" };
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(false);

        _warehouseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync((Backend.Domain.Entities.Warehouse?)null);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenValid_UpdatesAndSaves()
    {
        // Arrange
        var dto = new UpdateWarehouseDto { Id = 1, Code = "WH-NEW", Name = "New Name" };
        var existing = new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH-OLD", Name = "Old Name" };

        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(false);

        _warehouseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _warehouseRepository.Setup(repo => repo.UpdateAsync(existing)).Returns(Task.CompletedTask);
        _warehouseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        existing.Code.Should().Be("WH-NEW");
        existing.Name.Should().Be("New Name");
        _warehouseRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        _warehouseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_WhenCountMismatch_ReturnsBadRequest()
    {
        // Arrange
        var dtos = new List<UpdateWarehouseDto> { new() { Id = 1 }, new() { Id = 2 } };
        _warehouseRepository
            .Setup(repo => repo.FindByConditionAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(new List<Backend.Domain.Entities.Warehouse> { new() { Id = 1 } }); // only 1 found

        // Act
        var response = await _sut.UpdateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "Warehouse")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_WhenValid_UpdatesAllAndSaves()
    {
        // Arrange
        var dtos = new List<UpdateWarehouseDto>
        {
            new() { Id = 1, Code = "WH1-NEW" },
            new() { Id = 2, Code = "WH2-NEW" }
        };

        var list = new List<Backend.Domain.Entities.Warehouse>
        {
            new() { Id = 1, Code = "WH1-OLD" },
            new() { Id = 2, Code = "WH2-OLD" }
        };

        _warehouseRepository
            .Setup(repo => repo.FindByConditionAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>(), It.IsAny<bool>()))
            .ReturnsAsync(list);

        _warehouseRepository.Setup(repo => repo.UpdateListAsync(list)).Returns(Task.CompletedTask);
        _warehouseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var response = await _sut.UpdateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        list[0].Code.Should().Be("WH1-NEW");
        list[1].Code.Should().Be("WH2-NEW");
        _warehouseRepository.Verify(repo => repo.UpdateListAsync(list), Times.Once);
        _warehouseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    private void SetupWarehouses(List<Backend.Domain.Entities.Warehouse> list)
    {
        _warehouseRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>(),
                It.IsAny<bool>()
            ))
            .Returns((Expression<Func<Backend.Domain.Entities.Warehouse, bool>> predicate, bool _) =>
                list.AsQueryable().Where(predicate).BuildMock());
    }
}
