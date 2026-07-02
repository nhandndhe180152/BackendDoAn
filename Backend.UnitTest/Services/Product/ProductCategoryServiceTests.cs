using System;
using System.Linq.Expressions;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductCategories;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services.Product;

public class ProductCategoryServiceTests
{
    private readonly Mock<IProductCategoryRepository> _categoryRepository = new();
    private readonly ProductCategoryService _sut;

    public ProductCategoryServiceTests()
    {
        _sut = new ProductCategoryService(_categoryRepository.Object);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_NewCategory_CreatesAndSaves()
    {
        // Arrange
        ProductCategory? createdCategory = null;

        _categoryRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
            .ReturnsAsync(false);
        _categoryRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<ProductCategory>()))
            .Callback<ProductCategory>(category => createdCategory = category)
            .Returns(Task.CompletedTask);
        _categoryRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        var request = new CreateProductCategoryDto
        {
            Name = "Phu kien",
            Description = "Nhom phu kien",
            ParentCategoryId = null,
            TreeIds = "2",
            SortOrder = 1,
            CreatedBy = 1001
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        createdCategory.Should().NotBeNull();
        createdCategory!.Name.Should().Be("Phu kien");
        createdCategory.TreeIds.Should().Be("2");
        _categoryRepository.Verify(repo => repo.CreateAsync(It.IsAny<ProductCategory>()), Times.Once);
        _categoryRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        _categoryRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
            .ReturnsAsync(true);

        var request = new CreateProductCategoryDto
        {
            Name = "Phu kien",
            TreeIds = "2"
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _categoryRepository.Verify(repo => repo.CreateAsync(It.IsAny<ProductCategory>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_ExistingCategory_ReturnsDetailWithParentName()
    {
        // Arrange
        var parent = CreateCategory(id: 1, name: "San pham");
        var child = CreateCategory(id: 2, name: "Phu kien", parentId: 1, parentCategory: parent);

        SetupCategories([parent, child]);

        // Act
        var response = await _sut.GetByIdAsync(2);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeOfType<ProductCategoryDetailDto>().Subject;
        result.Id.Should().Be(2);
        result.Name.Should().Be("Phu kien");
        result.ParentName.Should().Be("San pham");
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_MissingCategory_ReturnsNotFound()
    {
        // Arrange
        SetupCategories([]);

        // Act
        var response = await _sut.GetByIdAsync(404);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Search")]
    public async Task GetPagedAsync_SearchQuery_FiltersByKeyword()
    {
        // Arrange
        var categories = new List<ProductCategory>
        {
            CreateCategory(id: 1, name: "Thoi trang", description: "Quan ao"),
            CreateCategory(id: 2, name: "Phu kien", description: "Day sac va tai nghe"),
            CreateCategory(id: 3, name: "Do uong", description: "Nuoc giai khat")
        };

        SetupCategories(categories);

        var query = new SearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            Keyword = "sac"
        };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeOfType<PagingData<ProductCategoryDetailDto>>().Subject;
        result.Total.Should().Be(3);
        result.TotalFiltered.Should().Be(1);
        result.DataSource.Should().ContainSingle(x => x.Name == "Phu kien");
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Search")]
    public async Task GetPagedAsync_ProductCategorySearchQuery_FiltersByParentId()
    {
        // Arrange
        var parent = CreateCategory(id: 1, name: "San pham");
        var categories = new List<ProductCategory>
        {
            parent,
            CreateCategory(id: 2, name: "Phu kien", parentId: 1, parentCategory: parent),
            CreateCategory(id: 3, name: "Thoi trang", parentId: 1, parentCategory: parent),
            CreateCategory(id: 4, name: "Khac", parentId: null)
        };

        SetupCategories(categories);

        var query = new ProductCategorySearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            ParentId = 1
        };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeOfType<PagingData<ProductCategoryDetailDto>>().Subject;
        result.Total.Should().Be(4);
        result.TotalFiltered.Should().Be(2);
        result.DataSource.Select(x => x.Name).Should().BeEquivalentTo(["Phu kien", "Thoi trang"]);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_ExistingCategory_UpdatesAndSaves()
    {
        // Arrange
        var category = CreateCategory(id: 2, name: "Cu");

        _categoryRepository.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(category);
        _categoryRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
            .ReturnsAsync(false);
        _categoryRepository.Setup(repo => repo.UpdateAsync(category)).Returns(Task.CompletedTask);
        _categoryRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        var request = new UpdateProductCategoryDto
        {
            Id = 2,
            Name = "Moi",
            Description = "Da cap nhat",
            ParentCategoryId = 1,
            TreeIds = "1,2",
            SortOrder = 5,
            UpdatedBy = 1001
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        category.Name.Should().Be("Moi");
        category.ParentCategoryId.Should().Be(1);
        category.SortOrder.Should().Be(5);
        _categoryRepository.Verify(repo => repo.UpdateAsync(category), Times.Once);
        _categoryRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var category = CreateCategory(id: 2, name: "Cu");

        _categoryRepository.Setup(repo => repo.GetByIdAsync(2)).ReturnsAsync(category);
        _categoryRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductCategory, bool>>>()))
            .ReturnsAsync(true);

        var request = new UpdateProductCategoryDto
        {
            Id = 2,
            Name = "Bi trung",
            TreeIds = "2"
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _categoryRepository.Verify(repo => repo.UpdateAsync(It.IsAny<ProductCategory>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryDeletes_ReturnsSuccess()
    {
        // Arrange
        _categoryRepository.Setup(repo => repo.SoftDeleteAsync(2)).ReturnsAsync(true);
        _categoryRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(2);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(true);
        _categoryRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductCategory")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryCannotDelete_ReturnsBadRequest()
    {
        // Arrange
        _categoryRepository.Setup(repo => repo.SoftDeleteAsync(404)).ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(404);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _categoryRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    private void SetupCategories(List<ProductCategory> categories)
    {
        _categoryRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<ProductCategory, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<ProductCategory, bool>> predicate, bool _) =>
                categories.AsQueryable().Where(predicate).BuildMock());
    }

    private static ProductCategory CreateCategory(
        int id,
        string name,
        string? description = null,
        int? parentId = null,
        ProductCategory? parentCategory = null)
    {
        return new ProductCategory
        {
            Id = id,
            Name = name,
            Description = description ?? $"{name} description",
            ParentCategoryId = parentId,
            ParentCategory = parentCategory,
            TreeIds = parentId.HasValue ? $"{parentId},{id}" : id.ToString(),
            SortOrder = id,
            IsDeleted = false,
            CreatedDate = DateTime.Now
        };
    }
}
