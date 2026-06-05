using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.QRCode;

public class QRCodeServiceTests
{
    private readonly Mock<IProductVariantRepository> _productVariantRepoMock = new();
    private readonly Mock<IStorageService> _storageServiceMock = new();
    private readonly Mock<IInventoryRepository> _inventoryRepoMock = new();
    private readonly QRCodeService _sut;

    public QRCodeServiceTests()
    {
        _sut = new QRCodeService(
            _productVariantRepoMock.Object, 
            _storageServiceMock.Object,
            _inventoryRepoMock.Object);
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GenerateQRCodeImage")]
    public async Task GenerateQRCodeImageAsync_ValidId_ReturnsPngBytes()
    {
        // Arrange
        var variant = new ProductVariant { Id = 1, SKU = "SKU-TEST-123", IsDeleted = false };
        _productVariantRepoMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(variant);

        // Act
        var result = await _sut.GenerateQRCodeImageAsync(1);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
        _productVariantRepoMock.Verify(r => r.GetByIdAsync(1), Times.Once);
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GenerateQRCodeImage")]
    public async Task GenerateQRCodeImageAsync_InvalidId_ThrowsKeyNotFoundException()
    {
        // Arrange
        _productVariantRepoMock.Setup(r => r.GetByIdAsync(999))
            .ReturnsAsync((ProductVariant?)null);

        // Act & Assert
        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.GenerateQRCodeImageAsync(999));
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GenerateQRLabelPdf")]
    public async Task GenerateQRLabelPdfAsync_ValidId_ReturnsPdfBytes()
    {
        // Arrange
        var variant = new ProductVariant
        {
            Id = 1,
            SKU = "SKU-TEST-123",
            Name = "Test Variant",
            SalePrice = 100000,
            AttributeValues = "Color: Black, Size: L",
            IsDeleted = false
        };

        var list = new List<ProductVariant> { variant };
        _productVariantRepoMock.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<ProductVariant, bool>>>(), It.IsAny<bool>()))
            .Returns(list.AsQueryable().BuildMock());

        _inventoryRepoMock.Setup(r => r.GetByProductVariantAsync(1))
            .ReturnsAsync(new List<Inventory>());

        // Act
        var result = await _sut.GenerateQRLabelPdfAsync(1, 50f, 30f);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GenerateBulkQRLabelsPdf")]
    public async Task GenerateBulkQRLabelsPdfAsync_ValidRequest_ReturnsCombinedPdfBytes()
    {
        // Arrange
        var variant1 = new ProductVariant { Id = 1, SKU = "SKU-1", Name = "V1", SalePrice = 50000 };
        var variant2 = new ProductVariant { Id = 2, SKU = "SKU-2", Name = "V2", SalePrice = 75000 };

        var list = new List<ProductVariant> { variant1, variant2 };
        _productVariantRepoMock.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<ProductVariant, bool>>>(), It.IsAny<bool>()))
            .Returns(list.AsQueryable().BuildMock());

        var inventories = new List<Inventory>();
        _inventoryRepoMock.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Inventory, bool>>>(), It.IsAny<bool>()))
            .Returns(inventories.AsQueryable().BuildMock());

        var request = new BatchQRLabelRequestDto
        {
            Items = new List<ProductVariantQuantityDto>
            {
                new() { ProductVariantId = 1, Quantity = 2 },
                new() { ProductVariantId = 2, Quantity = 1 }
            },
            WidthMm = 50f,
            HeightMm = 30f
        };

        // Act
        var result = await _sut.GenerateBulkQRLabelsPdfAsync(request);

        // Assert
        result.Should().NotBeNull();
        result.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GenerateAndSaveQRUrl")]
    public async Task GenerateAndSaveQRUrlAsync_ValidId_UploadsAndPersistsUrl()
    {
        // Arrange
        var variant = new ProductVariant { Id = 1, SKU = "SKU-TEST-123", IsDeleted = false };
        _productVariantRepoMock.Setup(r => r.GetByIdAsync(1))
            .ReturnsAsync(variant);

        var uploadResult = new FileUploadResult
        {
            Success = true,
            FilePath = "https://cloudinary.test/qrcode.png"
        };
        _storageServiceMock.Setup(s => s.UploadAsync(It.IsAny<IFormFile>(), "qrcodes"))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _sut.GenerateAndSaveQRUrlAsync(1);

        // Assert
        result.Should().Be("https://cloudinary.test/qrcode.png");
        variant.QRCode.Should().Be("https://cloudinary.test/qrcode.png");
        _productVariantRepoMock.Verify(r => r.UpdateAsync(variant), Times.Once);
        _productVariantRepoMock.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "SyncAllQRCodeUrls")]
    public async Task SyncAllQRCodeUrlsAsync_MissingCodes_SyncsThem()
    {
        // Arrange
        var variant1 = new ProductVariant { Id = 1, SKU = "SKU-1", QRCode = null, IsDeleted = false };
        var variant2 = new ProductVariant { Id = 2, SKU = "SKU-2", QRCode = "", IsDeleted = false };

        var list = new List<ProductVariant> { variant1, variant2 };
        _productVariantRepoMock.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<ProductVariant, bool>>>(), It.IsAny<bool>()))
            .Returns(list.AsQueryable().BuildMock());

        _productVariantRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(variant1);
        _productVariantRepoMock.Setup(r => r.GetByIdAsync(2)).ReturnsAsync(variant2);

        var uploadResult = new FileUploadResult
        {
            Success = true,
            FilePath = "https://cloudinary.test/qrcode.png"
        };
        _storageServiceMock.Setup(s => s.UploadAsync(It.IsAny<IFormFile>(), "qrcodes"))
            .ReturnsAsync(uploadResult);

        // Act
        var result = await _sut.SyncAllQRCodeUrlsAsync();

        // Assert
        result.Should().Be(2);
        variant1.QRCode.Should().Be("https://cloudinary.test/qrcode.png");
        variant2.QRCode.Should().Be("https://cloudinary.test/qrcode.png");
        _productVariantRepoMock.Verify(r => r.SaveChangesAsync(), Times.Exactly(2));
    }

    [Fact]
    [Trait("Service", "QRCode")]
    [Trait("Method", "GetVietnameseFont")]
    public void GetVietnameseFont_LoadsVietnameseFontSuccessfully()
    {
        // Act
        var method = typeof(QRCodeService).GetMethod("GetVietnameseFont", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var font = method!.Invoke(_sut, null) as iText.Kernel.Font.PdfFont;

        // Assert
        font.Should().NotBeNull("A font with Vietnamese character support should be loaded from the system paths.");
    }
}

