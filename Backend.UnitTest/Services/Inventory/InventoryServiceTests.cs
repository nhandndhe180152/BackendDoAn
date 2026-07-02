using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Application.DTOs.Inventories;
using Backend.Application.Implements;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Inventory;

public class InventoryServiceTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly InventoryService _sut;

    public InventoryServiceTests()
    {
        _sut = new InventoryService(_inventoryRepository.Object);
    }

    [Fact]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsSuccess()
    {
        // Arrange
        var parameters = new InventoryDTParameters();
        var mockResult = new DTResult<InventoryAggregate>
        {
            draw = 1,
            recordsTotal = 10,
            recordsFiltered = 10,
            data = new List<InventoryAggregate>()
        };

        _inventoryRepository
            .Setup(repo => repo.GetPagedAsync(parameters))
            .ReturnsAsync(mockResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        response.Resources.Should().BeEquivalentTo(mockResult);
    }

    [Fact]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _inventoryRepository
            .Setup(repo => repo.GetByIdDetailAsync(1))
            .ReturnsAsync((Backend.Domain.Entities.Inventory?)null);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenExists_ReturnsInventoryDto()
    {
        // Arrange
        var entity = new Backend.Domain.Entities.Inventory
        {
            Id = 1,
            ProductVariantId = 10,
            WarehouseId = 2,
            QuantityOnHand = 100
        };

        _inventoryRepository
            .Setup(repo => repo.GetByIdDetailAsync(1))
            .ReturnsAsync(entity);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var dto = response.Resources as InventoryDto;
        dto.Should().NotBeNull();
        dto!.Id.Should().Be(1);
        dto.QuantityOnHand.Should().Be(100);
    }

    [Fact]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetByProductVariant")]
    public async Task GetByProductVariantAsync_WhenInvalidId_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.GetByProductVariantAsync(0);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetByProductVariant")]
    public async Task GetByProductVariantAsync_WhenValidId_ReturnsList()
    {
        // Arrange
        var list = new List<Backend.Domain.Entities.Inventory>
        {
            new() { Id = 1, ProductVariantId = 5, QuantityOnHand = 10 },
            new() { Id = 2, ProductVariantId = 5, QuantityOnHand = 20 }
        };

        _inventoryRepository
            .Setup(repo => repo.GetByProductVariantAsync(5))
            .ReturnsAsync(list);

        // Act
        var response = await _sut.GetByProductVariantAsync(5);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var dtos = response.Resources as List<InventoryDto>;
        dtos.Should().NotBeNull();
        dtos.Should().HaveCount(2);
    }

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-10, 50)]
    [InlineData(300, 200)]
    [InlineData(100, 100)]
    [Trait("Service", "Inventory")]
    [Trait("Method", "GetLowStock")]
    public async Task GetLowStockAsync_ClampsLimitCorrectly(int inputLimit, int expectedLimit)
    {
        // Arrange
        _inventoryRepository
            .Setup(repo => repo.GetLowStockAsync(It.IsAny<int?>(), expectedLimit))
            .ReturnsAsync(new List<Backend.Domain.Entities.Inventory>());

        // Act
        var response = await _sut.GetLowStockAsync(1, inputLimit);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        _inventoryRepository.Verify(repo => repo.GetLowStockAsync(1, expectedLimit), Times.Once);
    }
}
