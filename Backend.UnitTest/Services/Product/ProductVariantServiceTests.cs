using System;
using System.Linq.Expressions;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services.Product;

public class ProductVariantServiceTests
{
    private readonly Mock<IProductVariantRepository> _productVariantRepository = new();
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly Mock<IProductCategoryRepository> _productCategoryRepository = new();
    private readonly Mock<IProductAttributeRepository> _productAttributeRepository = new();
    private readonly Mock<IRepositoryBase<FileUpload, int>> _fileUploadRepository = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.UnitOfMeasure, int>> _uomRepository = new();
    private readonly Mock<IStorageService> _storageService = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.Inventory, int>> _inventoryRepository = new();
    private readonly Mock<IRepositoryBase<InboundOrderItem, int>> _inboundOrderItemRepository = new();
    private readonly Mock<IRepositoryBase<OutboundOrderItem, int>> _outboundOrderItemRepository = new();
    private readonly Mock<IRepositoryBase<StockTakeItem, int>> _stockTakeItemRepository = new();
    private readonly Mock<IQRCodeService> _qrCodeService = new();

    private readonly ProductVariantService _sut;

    public ProductVariantServiceTests()
    {
        _sut = new ProductVariantService(
            _productVariantRepository.Object,
            _productRepository.Object,
            _productCategoryRepository.Object,
            _productAttributeRepository.Object,
            _fileUploadRepository.Object,
            _uomRepository.Object,
            _storageService.Object,
            _inventoryRepository.Object,
            _inboundOrderItemRepository.Object,
            _outboundOrderItemRepository.Object,
            _stockTakeItemRepository.Object,
            _qrCodeService.Object);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_ExistingSku_ReturnsProductAndStockQuantities()
    {
        // Arrange
        var variant = CreateVariant(id: 5, sku: "AP-RED-M");
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            new() { ProductVariantId = 5, QuantityOnHand = 12, QuantityReserved = 2 },
            new() { ProductVariantId = 5, QuantityOnHand = 8, QuantityReserved = 1 }
        };

        SetupProductVariants([variant]);
        SetupInventories(inventories);

        // Act
        var response = await _sut.CheckSkuAsync("AP-RED-M");

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.ProductVariant.Id.Should().Be(5);
        result.ProductVariant.SKU.Should().Be("AP-RED-M");
        result.QuantityOnHand.Should().Be(20);
        result.QuantityReserved.Should().Be(3);
        result.QuantityAvailable.Should().Be(17);
        result.BelongsToDocument.Should().BeFalse();
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_MissingSku_ReturnsNotFound()
    {
        // Arrange
        SetupProductVariants([]);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-NOT-FOUND");

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        response.Code.Should().Be("CMN_404");
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_StockTakeDocument_ReturnsDocumentQuantities()
    {
        // Arrange
        var variant = CreateVariant(id: 5, sku: "AP-RED-M");
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            new() { ProductVariantId = 5, QuantityOnHand = 12, QuantityReserved = 0 }
        };
        var stockTakeItems = new List<StockTakeItem>
        {
            new()
            {
                StockTakeId = 10,
                ProductVariantId = 5,
                SystemQuantity = 12,
                ActualQuantity = 9,
                QRScanned = true
            }
        };

        SetupProductVariants([variant]);
        SetupInventories(inventories);
        SetupStockTakeItems(stockTakeItems);

        // Act
        var response = await _sut.CheckSkuAsync(
            sku: "AP-RED-M",
            documentType: "StockTake",
            documentId: 10);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.BelongsToDocument.Should().BeTrue();
        result.DocumentQuantityOrdered.Should().Be(12);
        result.DocumentQuantityProcessed.Should().Be(9);
        result.IsQrScanned.Should().BeTrue();
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_NoInventory_ReturnsZeroStock()
    {
        // Arrange
        var variant = CreateVariant(id: 7, sku: "SKU-NO-STOCK");

        SetupProductVariants([variant]);
        SetupInventories([]);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-NO-STOCK");

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.QuantityOnHand.Should().Be(0);
        result.QuantityReserved.Should().Be(0);
        result.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_AllStockReserved_ReturnsZeroAvailable()
    {
        // Arrange
        var variant = CreateVariant(id: 8, sku: "SKU-RESERVED");
        var inventories = new List<Backend.Domain.Entities.Inventory>
        {
            new() { ProductVariantId = 8, QuantityOnHand = 10, QuantityReserved = 10 }
        };

        SetupProductVariants([variant]);
        SetupInventories(inventories);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-RESERVED");

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.QuantityOnHand.Should().Be(10);
        result.QuantityReserved.Should().Be(10);
        result.QuantityAvailable.Should().Be(0);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_PurchaseOrderDocument_ReturnsOrderedAndReceivedQuantities()
    {
        // Arrange
        var variant = CreateVariant(id: 9, sku: "SKU-PO");
        var purchaseOrderItems = new List<InboundOrderItem>
        {
            new()
            {
                InboundOrderId = 15,
                ProductVariantId = 9,
                QuantityOrdered = 40,
                QuantityReceived = 12,
                QRScanned = false
            }
        };

        SetupProductVariants([variant]);
        SetupInventories([]);
        SetupInboundOrderItems(purchaseOrderItems);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-PO", "PurchaseOrder", 15);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.BelongsToDocument.Should().BeTrue();
        result.DocumentQuantityOrdered.Should().Be(40);
        result.DocumentQuantityProcessed.Should().Be(12);
        result.IsQrScanned.Should().BeFalse();
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_SalesOrderDocument_ReturnsOrderedAndPickedQuantities()
    {
        // Arrange
        var variant = CreateVariant(id: 10, sku: "SKU-SO");
        var salesOrderItems = new List<OutboundOrderItem>
        {
            new()
            {
                OutboundOrderId = 20,
                ProductVariantId = 10,
                QuantityOrdered = 6,
                QuantityPicked = 4,
                QRScanned = true
            }
        };

        SetupProductVariants([variant]);
        SetupInventories([]);
        SetupOutboundOrderItems(salesOrderItems);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-SO", "SalesOrder", 20);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.BelongsToDocument.Should().BeTrue();
        result.DocumentQuantityOrdered.Should().Be(6);
        result.DocumentQuantityProcessed.Should().Be(4);
        result.IsQrScanned.Should().BeTrue();
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "CheckSku")]
    public async Task CheckSkuAsync_ProductNotInDocument_KeepsBelongsToDocumentFalse()
    {
        // Arrange
        var variant = CreateVariant(id: 11, sku: "SKU-OUTSIDE-DOC");

        SetupProductVariants([variant]);
        SetupInventories([]);
        SetupInboundOrderItems([]);

        // Act
        var response = await _sut.CheckSkuAsync("SKU-OUTSIDE-DOC", "PurchaseOrder", 99);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<SkuCheckResultDto>().Subject;
        result.BelongsToDocument.Should().BeFalse();
        result.DocumentQuantityOrdered.Should().BeNull();
        result.DocumentQuantityProcessed.Should().BeNull();
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "ConfirmScan")]
    public async Task ConfirmScanAsync_PurchaseOrder_AddsReceivedQuantityAndSaves()
    {
        // Arrange
        var variant = CreateVariant(id: 12, sku: "SKU-CONFIRM-PO");
        var poItem = new InboundOrderItem
        {
            InboundOrderId = 30,
            ProductVariantId = 12,
            QuantityOrdered = 20,
            QuantityReceived = 5,
            QRScanned = false
        };

        SetupProductVariants([variant]);
        SetupInboundOrderItems([poItem]);

        var request = new ConfirmScanRequestDto
        {
            Sku = "SKU-CONFIRM-PO",
            DocumentType = "PurchaseOrder",
            DocumentId = 30,
            Quantity = 3
        };

        // Act
        var response = await _sut.ConfirmScanAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        poItem.QRScanned.Should().BeTrue();
        poItem.QuantityReceived.Should().Be(8);
        _inboundOrderItemRepository.Verify(repo => repo.UpdateAsync(poItem), Times.Once);
        _inboundOrderItemRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "ConfirmScan")]
    public async Task ConfirmScanAsync_SalesOrder_AddsPickedQuantityAndSaves()
    {
        // Arrange
        var variant = CreateVariant(id: 13, sku: "SKU-CONFIRM-SO");
        var soItem = new OutboundOrderItem
        {
            OutboundOrderId = 31,
            ProductVariantId = 13,
            QuantityOrdered = 10,
            QuantityPicked = 2,
            QRScanned = false
        };

        SetupProductVariants([variant]);
        SetupOutboundOrderItems([soItem]);

        var request = new ConfirmScanRequestDto
        {
            Sku = "SKU-CONFIRM-SO",
            DocumentType = "SalesOrder",
            DocumentId = 31,
            Quantity = 4
        };

        // Act
        var response = await _sut.ConfirmScanAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        soItem.QRScanned.Should().BeTrue();
        soItem.QuantityPicked.Should().Be(6);
        _outboundOrderItemRepository.Verify(repo => repo.UpdateAsync(soItem), Times.Once);
        _outboundOrderItemRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "ConfirmScan")]
    public async Task ConfirmScanAsync_StockTake_AddsActualQuantityAndSaves()
    {
        // Arrange
        var variant = CreateVariant(id: 14, sku: "SKU-CONFIRM-ST");
        var stItem = new StockTakeItem
        {
            StockTakeId = 32,
            ProductVariantId = 14,
            SystemQuantity = 15,
            ActualQuantity = null,
            QRScanned = false
        };

        SetupProductVariants([variant]);
        SetupStockTakeItems([stItem]);

        var request = new ConfirmScanRequestDto
        {
            Sku = "SKU-CONFIRM-ST",
            DocumentType = "StockTake",
            DocumentId = 32,
            Quantity = 7
        };

        // Act
        var response = await _sut.ConfirmScanAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        stItem.QRScanned.Should().BeTrue();
        stItem.ActualQuantity.Should().Be(7);
        _stockTakeItemRepository.Verify(repo => repo.UpdateAsync(stItem), Times.Once);
        _stockTakeItemRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductVariant")]
    [Trait("Method", "ConfirmScan")]
    public async Task ConfirmScanAsync_UnsupportedDocumentType_ReturnsBadRequest()
    {
        // Arrange
        var variant = CreateVariant(id: 15, sku: "SKU-UNSUPPORTED");

        SetupProductVariants([variant]);

        var request = new ConfirmScanRequestDto
        {
            Sku = "SKU-UNSUPPORTED",
            DocumentType = "OtherDocument",
            DocumentId = 1,
            Quantity = 1
        };

        // Act
        var response = await _sut.ConfirmScanAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    private void SetupProductVariants(List<ProductVariant> variants)
    {
        _productVariantRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<ProductVariant, bool>>>(),
                It.IsAny<bool>()))
            .Returns(variants.AsQueryable().BuildMock());
    }

    private void SetupInventories(List<Backend.Domain.Entities.Inventory> inventories)
    {
        _inventoryRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<Backend.Domain.Entities.Inventory, bool>>>(),
                It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());
    }

    private void SetupStockTakeItems(List<StockTakeItem> items)
    {
        _stockTakeItemRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<StockTakeItem, bool>>>(),
                It.IsAny<bool>()))
            .Returns(items.AsQueryable().BuildMock());
    }

    private void SetupInboundOrderItems(List<InboundOrderItem> items)
    {
        _inboundOrderItemRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<InboundOrderItem, bool>>>(),
                It.IsAny<bool>()))
            .Returns(items.AsQueryable().BuildMock());
    }

    private void SetupOutboundOrderItems(List<OutboundOrderItem> items)
    {
        _outboundOrderItemRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<OutboundOrderItem, bool>>>(),
                It.IsAny<bool>()))
            .Returns(items.AsQueryable().BuildMock());
    }

    private static ProductVariant CreateVariant(int id, string sku)
    {
        return new ProductVariant
        {
            Id = id,
            Name = "Ao polo - Do - Size M",
            SKU = sku,
            ProductId = 2,
            Product = new Backend.Domain.Entities.Product { Id = 2, Name = "Ao polo" },
            UnitOfMeasureId = 1,
            UnitOfMeasure = new Backend.Domain.Entities.UnitOfMeasure { Id = 1, Name = "Cai" },
            IsActive = true,
            IsDeleted = false,
            CreatedDate = DateTime.Now
        };
    }
}
