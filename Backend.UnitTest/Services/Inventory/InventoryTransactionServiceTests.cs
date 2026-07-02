using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Storage;
using Backend.Application.Constants;
using Backend.Application.DTOs.InventoryTransactions;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Inventory;

public class InventoryTransactionServiceTests
{
    private readonly Mock<IInventoryRepository> _inventoryRepository = new();
    private readonly Mock<IInventoryTransactionRepository> _inventoryTransactionRepository = new();
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly InventoryTransactionService _sut;

    public InventoryTransactionServiceTests()
    {
        _sut = new InventoryTransactionService(
            _inventoryRepository.Object,
            _inventoryTransactionRepository.Object,
            _productVariantRepository.Object,
            _httpContextAccessor.Object
        );
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsResult()
    {
        // Arrange
        var parameters = new InventoryTransactionDTParameters();
        var mockResult = new DTResult<InventoryTransactionAggregate> { draw = 1 };
        _inventoryTransactionRepository
            .Setup(repo => repo.GetPagedAsync(parameters))
            .ReturnsAsync(mockResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(mockResult);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _inventoryTransactionRepository
            .Setup(repo => repo.GetByIdDetailAsync(1))
            .ReturnsAsync((InventoryTransaction?)null);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
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
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "GetByProductVariant")]
    public async Task GetByProductVariantAsync_WhenValid_ReturnsSuccess()
    {
        // Arrange
        var list = new List<InventoryTransaction> { new() { Id = 1, ProductVariantId = 5 } };
        _inventoryTransactionRepository
            .Setup(repo => repo.GetByProductVariantAsync(5, 100))
            .ReturnsAsync(list);

        // Act
        var response = await _sut.GetByProductVariantAsync(5);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenProductVariantIdInvalid_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.ManualAdjustmentAsync(new ManualInventoryAdjustmentDto { ProductVariantId = 0 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenWarehouseIdInvalid_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.ManualAdjustmentAsync(new ManualInventoryAdjustmentDto { ProductVariantId = 1, WarehouseId = 0 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenNoQuantityProvided_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.ManualAdjustmentAsync(new ManualInventoryAdjustmentDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1,
            NewQuantityOnHand = null,
            AdjustmentQuantity = null
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenNewQuantityNegative_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.ManualAdjustmentAsync(new ManualInventoryAdjustmentDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1,
            NewQuantityOnHand = -5
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenAdjustmentQuantityZero_ReturnsBadRequest()
    {
        // Act
        var response = await _sut.ManualAdjustmentAsync(new ManualInventoryAdjustmentDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1,
            AdjustmentQuantity = 0
        });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenResultQuantityNegative_ReturnsBadRequest()
    {
        // Arrange
        var request = new ManualInventoryAdjustmentDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1,
            AdjustmentQuantity = -10
        };
        var currentInventory = new Backend.Domain.Entities.Inventory { ProductVariantId = 1, WarehouseId = 1, QuantityOnHand = 5 };
        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync(currentInventory);

        // Act
        var response = await _sut.ManualAdjustmentAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ManualAdjustment")]
    public async Task ManualAdjustmentAsync_WhenValid_PerformsAdjustment()
    {
        // Arrange
        var request = new ManualInventoryAdjustmentDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1,
            NewQuantityOnHand = 15,
            Reason = "Manual test adjust"
        };

        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 10 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        var currentInventory = new Backend.Domain.Entities.Inventory { Id = 100, ProductVariantId = 1, WarehouseId = 1, QuantityOnHand = 5 };
        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync(currentInventory);

        _inventoryRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Backend.Domain.Entities.Inventory>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.ManualAdjustmentAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        currentInventory.QuantityOnHand.Should().Be(15);
        _inventoryRepository.Verify(repo => repo.UpdateAsync(currentInventory), Times.Once);
        _inventoryTransactionRepository.Verify(repo => repo.CreateAsync(It.IsAny<InventoryTransaction>()), Times.Once);
        _inventoryRepository.Verify(repo => repo.EndTransactionAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ReceiveStock")]
    public async Task ReceiveStockAsync_WhenVariantNotFound_ReturnsNotFound()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 10 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync((ProductVariant?)null);

        // Act
        var response = await _sut.ReceiveStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ReceiveStock")]
    public async Task ReceiveStockAsync_WhenInventoryIsNull_CreatesNewInventory()
    {
        // Arrange
        var request = new StockMovementRequestDto 
        { 
            ProductVariantId = 1, 
            WarehouseId = 1, 
            Quantity = 10,
            ReferenceType = "PURCHASE_ORDER",
            ReferenceId = 10,
            CostPrice = 20
        };

        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 15 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync((Backend.Domain.Entities.Inventory?)null);

        _inventoryRepository.Setup(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Inventory>())).Returns(Task.CompletedTask);
        _inventoryRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryRepository.Setup(repo => repo.UpdateAsync(It.IsAny<Backend.Domain.Entities.Inventory>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.ReceiveStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        // Inventory không còn field PurchaseOrderId; xác minh inventory mới đúng variant + warehouse
        _inventoryRepository.Verify(repo => repo.CreateAsync(It.Is<Backend.Domain.Entities.Inventory>(i => i.ProductVariantId == 1 && i.WarehouseId == 1)), Times.Once);
        _inventoryRepository.Verify(repo => repo.UpdateAsync(It.Is<Backend.Domain.Entities.Inventory>(i => i.QuantityOnHand == 10)), Times.Once);
        _inventoryTransactionRepository.Verify(repo => repo.CreateAsync(It.Is<InventoryTransaction>(t => t.Quantity == 10 && t.BeforeQuantity == 0 && t.AfterQuantity == 10)), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "DispatchStock")]
    public async Task DispatchStockAsync_WhenInventoryIsNull_ReturnsBadRequest()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 10 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 15 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync((Backend.Domain.Entities.Inventory?)null);

        _inventoryRepository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.DispatchStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _inventoryRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "DispatchStock")]
    public async Task DispatchStockAsync_WhenInsufficientStock_ReturnsBadRequest()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 10 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 15 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        var inventory = new Backend.Domain.Entities.Inventory { ProductVariantId = 1, WarehouseId = 1, QuantityOnHand = 5, QuantityReserved = 0 };
        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync(inventory);

        _inventoryRepository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.DispatchStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _inventoryRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "DispatchStock")]
    public async Task DispatchStockAsync_WhenValid_DispatchesStock()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 5 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 15 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        var inventory = new Backend.Domain.Entities.Inventory { ProductVariantId = 1, WarehouseId = 1, QuantityOnHand = 10, QuantityReserved = 2 };
        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync(inventory);

        _inventoryRepository.Setup(repo => repo.UpdateAsync(inventory)).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.DispatchStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        inventory.QuantityOnHand.Should().Be(5);
        _inventoryTransactionRepository.Verify(repo => repo.CreateAsync(It.Is<InventoryTransaction>(t => t.Quantity == -5)), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "AdjustStock")]
    public async Task AdjustStockAsync_WhenValid_AdjustsStock()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 0 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);

        var productVariant = new ProductVariant { Id = 1, CostPrice = 15 };
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ReturnsAsync(productVariant);

        var inventory = new Backend.Domain.Entities.Inventory { ProductVariantId = 1, WarehouseId = 1, QuantityOnHand = 10 };
        _inventoryRepository
            .Setup(repo => repo.GetByVariantWarehouseLocationAsync(1, 1, null))
            .ReturnsAsync(inventory);

        _inventoryRepository.Setup(repo => repo.UpdateAsync(inventory)).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.CreateAsync(It.IsAny<InventoryTransaction>())).Returns(Task.CompletedTask);
        _inventoryTransactionRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);
        _inventoryRepository.Setup(repo => repo.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.AdjustStockAsync(request, 8);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        inventory.QuantityOnHand.Should().Be(8);
        _inventoryTransactionRepository.Verify(repo => repo.CreateAsync(It.Is<InventoryTransaction>(t => t.Quantity == -2)), Times.Once);
    }

    [Fact]
    [Trait("Service", "InventoryTransaction")]
    [Trait("Method", "ReceiveStock")]
    public async Task ReceiveStockAsync_WhenExceptionOccurs_RollsBackAndReturnsInternalServerError()
    {
        // Arrange
        var request = new StockMovementRequestDto { ProductVariantId = 1, WarehouseId = 1, Quantity = 10 };
        var mockTx = new Mock<IDbContextTransaction>();
        _inventoryRepository.Setup(repo => repo.BeginTransactionAsync()).ReturnsAsync(mockTx.Object);
        _productVariantRepository.Setup(repo => repo.GetActiveByIdAsync(1)).ThrowsAsync(new Exception("Database connection failed"));
        _inventoryRepository.Setup(repo => repo.RollbackTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var response = await _sut.ReceiveStockAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(500);
        _inventoryRepository.Verify(repo => repo.RollbackTransactionAsync(), Times.Once);
    }
}
