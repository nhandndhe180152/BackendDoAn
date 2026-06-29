using System;
using System.Linq.Expressions;
using Backend.Application.DTOs.Products;
using Backend.Application.Implements;
using Backend.Application.Constants;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using DomainProduct = Backend.Domain.Entities.Product;

namespace Backend.UnitTest.Services.Product;

public class ProductServiceTests
{
    private readonly Mock<IProductRepository> _productRepository = new();
    private readonly ProductService _sut;

    public ProductServiceTests()
    {
        _sut = new ProductService(_productRepository.Object);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_NewProduct_CreatesAndSaves()
    {
        // Arrange
        DomainProduct? createdProduct = null;

        _productRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<DomainProduct, bool>>>()))
            .ReturnsAsync(false);

        _productRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<DomainProduct>()))
            .Callback<DomainProduct>(product => createdProduct = product)
            .Returns(Task.CompletedTask);

        _productRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        var request = new CreateProductDto
        {
            Name = "Ao thun",
            Description = "Ao cotton",
            ProductCategoryId = 2,
            IsActive = true,
            CreatedBy = 1001
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        createdProduct.Should().NotBeNull();
        createdProduct!.Name.Should().Be("Ao thun");
        createdProduct.ProductCategoryId.Should().Be(2);
        createdProduct.IsActive.Should().BeTrue();
        _productRepository.Verify(repo => repo.CreateAsync(It.IsAny<DomainProduct>()), Times.Once);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        _productRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<DomainProduct, bool>>>()))
            .ReturnsAsync(true);

        var request = new CreateProductDto
        {
            Name = "Ao thun",
            ProductCategoryId = 2,
            IsActive = true
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _productRepository.Verify(repo => repo.CreateAsync(It.IsAny<DomainProduct>()), Times.Never);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_ExistingProduct_ReturnsProductDetail()
    {
        // Arrange
        var products = new List<DomainProduct>
        {
            CreateProduct(id: 1, name: "Ao polo", categoryId: 3, categoryName: "Thoi trang")
        };

        SetupProducts(products);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<ProductDetailDto>().Subject;
        result.Id.Should().Be(1);
        result.Name.Should().Be("Ao polo");
        result.ProductCategoryName.Should().Be("Thoi trang");
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_MissingProduct_ReturnsNotFound()
    {
        // Arrange
        SetupProducts([]);

        // Act
        var response = await _sut.GetByIdAsync(999);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Search")]
    public async Task GetPagedAsync_SearchQuery_FiltersByKeywordAndPaginates()
    {
        // Arrange
        var products = new List<DomainProduct>
        {
            CreateProduct(id: 1, name: "Ao polo", categoryId: 1, categoryName: "Thoi trang"),
            CreateProduct(id: 2, name: "Cap sac Type-C", categoryId: 2, categoryName: "Phu kien"),
            CreateProduct(id: 3, name: "Tai nghe bluetooth", categoryId: 2, categoryName: "Phu kien")
        };

        SetupProducts(products);

        var query = new SearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            Keyword = "phu kien"
        };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<PagingData<ProductDetailDto>>().Subject;
        result.Total.Should().Be(3);
        result.TotalFiltered.Should().Be(2);
        result.DataSource.Should().HaveCount(2);
        result.DataSource.Select(x => x.Name).Should().Contain(["Cap sac Type-C", "Tai nghe bluetooth"]);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Search")]
    public async Task GetPagedAsync_ProductSearchQuery_FiltersByCategoryAndActiveStatus()
    {
        // Arrange
        var products = new List<DomainProduct>
        {
            CreateProduct(id: 1, name: "Ao polo", categoryId: 1, categoryName: "Thoi trang", isActive: true),
            CreateProduct(id: 2, name: "Cap sac Type-C", categoryId: 2, categoryName: "Phu kien", isActive: true),
            CreateProduct(id: 3, name: "Cable cu", categoryId: 2, categoryName: "Phu kien", isActive: false)
        };

        SetupProducts(products);

        var query = new ProductSearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            ProductCategoryId = 2,
            IsActive = true
        };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();

        var result = response.Resources.Should().BeOfType<PagingData<ProductDetailDto>>().Subject;
        result.Total.Should().Be(3);
        result.TotalFiltered.Should().Be(1);
        result.DataSource.Should().ContainSingle();
        result.DataSource.Single().Name.Should().Be("Cap sac Type-C");
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_ExistingProduct_UpdatesAndSaves()
    {
        // Arrange
        var product = CreateProduct(id: 1, name: "Ao cu", categoryId: 1, categoryName: "Thoi trang");
        DomainProduct? updatedProduct = null;

        _productRepository
            .Setup(repo => repo.GetByIdAsync(1))
            .ReturnsAsync(product);

        _productRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<DomainProduct, bool>>>()))
            .ReturnsAsync(false);

        _productRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<DomainProduct>()))
            .Callback<DomainProduct>(item => updatedProduct = item)
            .Returns(Task.CompletedTask);

        _productRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        var request = new UpdateProductDto
        {
            Id = 1,
            Name = "Ao moi",
            Description = "Da sua",
            ProductCategoryId = 2,
            IsActive = false,
            UpdatedBy = 1001
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        updatedProduct.Should().NotBeNull();
        updatedProduct!.Name.Should().Be("Ao moi");
        updatedProduct.ProductCategoryId.Should().Be(2);
        updatedProduct.IsActive.Should().BeFalse();
        _productRepository.Verify(repo => repo.UpdateAsync(product), Times.Once);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_MissingProduct_ReturnsNotFound()
    {
        // Arrange
        _productRepository
            .Setup(repo => repo.GetByIdAsync(999))
            .ReturnsAsync((DomainProduct?)null);

        var request = new UpdateProductDto
        {
            Id = 999,
            Name = "Khong ton tai",
            ProductCategoryId = 1,
            IsActive = true
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        _productRepository.Verify(repo => repo.UpdateAsync(It.IsAny<DomainProduct>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var product = CreateProduct(id: 1, name: "Ao cu", categoryId: 1, categoryName: "Thoi trang");

        _productRepository
            .Setup(repo => repo.GetByIdAsync(1))
            .ReturnsAsync(product);

        _productRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<DomainProduct, bool>>>()))
            .ReturnsAsync(true);

        var request = new UpdateProductDto
        {
            Id = 1,
            Name = "Ten bi trung",
            ProductCategoryId = 1,
            IsActive = true
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _productRepository.Verify(repo => repo.UpdateAsync(It.IsAny<DomainProduct>()), Times.Never);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryDeletes_ReturnsSuccess()
    {
        // Arrange
        _productRepository
            .Setup(repo => repo.SoftDeleteAsync(1))
            .ReturnsAsync(true);

        _productRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(true);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Product")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryCannotDelete_ReturnsBadRequest()
    {
        // Arrange
        _productRepository
            .Setup(repo => repo.SoftDeleteAsync(999))
            .ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(999);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _productRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    private void SetupProducts(List<DomainProduct> products)
    {
        _productRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<DomainProduct, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<DomainProduct, bool>> predicate, bool _) =>
                products.AsQueryable().Where(predicate).BuildMock());
    }

    private static DomainProduct CreateProduct(
        int id,
        string name,
        int categoryId,
        string categoryName,
        bool isActive = true,
        bool isDeleted = false)
    {
        return new DomainProduct
        {
            Id = id,
            Name = name,
            Description = $"{name} description",
            ProductCategoryId = categoryId,
            ProductCategory = new ProductCategory
            {
                Id = categoryId,
                Name = categoryName,
                TreeIds = categoryId.ToString(),
                IsDeleted = false
            },
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedDate = DateTime.Now
        };
    }
}
