using System;
using System.Linq.Expressions;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductAttributes;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services.Product;

public class ProductAttributeServiceTests
{
    private readonly Mock<IProductAttributeRepository> _attributeRepository = new();
    private readonly ProductAttributeService _sut;

    public ProductAttributeServiceTests()
    {
        _sut = new ProductAttributeService(_attributeRepository.Object);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_NewAttribute_CreatesAndSaves()
    {
        // Arrange
        ProductAttribute? createdAttribute = null;

        _attributeRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductAttribute, bool>>>()))
            .ReturnsAsync(false);
        _attributeRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<ProductAttribute>()))
            .Callback<ProductAttribute>(attribute => createdAttribute = attribute)
            .Returns(Task.CompletedTask);
        _attributeRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        var request = new CreateProductAttributeDto
        {
            Name = "Mau sac",
            Description = "Thuoc tinh mau",
            CreatedBy = 1001
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        createdAttribute.Should().NotBeNull();
        createdAttribute!.Name.Should().Be("Mau sac");
        _attributeRepository.Verify(repo => repo.CreateAsync(It.IsAny<ProductAttribute>()), Times.Once);
        _attributeRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        _attributeRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductAttribute, bool>>>()))
            .ReturnsAsync(true);

        var request = new CreateProductAttributeDto
        {
            Name = "Mau sac"
        };

        // Act
        var response = await _sut.CreateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _attributeRepository.Verify(repo => repo.CreateAsync(It.IsAny<ProductAttribute>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_ReturnsNotDeletedAttributes()
    {
        // Arrange
        SetupAttributes([
            CreateAttribute(id: 1, name: "Mau sac"),
            CreateAttribute(id: 2, name: "Size"),
            CreateAttribute(id: 3, name: "Da xoa", isDeleted: true)
        ]);

        // Act
        var response = await _sut.GetAllAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeAssignableTo<IEnumerable<ProductAttributeDetailDto>>().Subject.ToList();
        result.Should().HaveCount(2);
        result.Select(x => x.Name).Should().Contain(["Mau sac", "Size"]);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_ExistingAttribute_ReturnsDetail()
    {
        // Arrange
        var attribute = CreateAttribute(id: 1, name: "Mau sac");
        _attributeRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(attribute);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeOfType<ProductAttributeDetailDto>().Subject;
        result.Id.Should().Be(1);
        result.Name.Should().Be("Mau sac");
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_MissingAttribute_ReturnsNotFound()
    {
        // Arrange
        _attributeRepository.Setup(repo => repo.GetByIdAsync(404)).ReturnsAsync((ProductAttribute?)null);

        // Act
        var response = await _sut.GetByIdAsync(404);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Search")]
    public async Task GetPagedAsync_SearchQuery_FiltersByKeyword()
    {
        // Arrange
        SetupAttributes([
            CreateAttribute(id: 1, name: "Mau sac", description: "Do xanh vang"),
            CreateAttribute(id: 2, name: "Size", description: "S M L"),
            CreateAttribute(id: 3, name: "Chat lieu", description: "Cotton")
        ]);

        var query = new SearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            Keyword = "cotton"
        };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var result = response.Resources.Should().BeOfType<PagingData<ProductAttributeDetailDto>>().Subject;
        result.Total.Should().Be(3);
        result.TotalFiltered.Should().Be(1);
        result.DataSource.Should().ContainSingle(x => x.Name == "Chat lieu");
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_ExistingAttribute_UpdatesAndSaves()
    {
        // Arrange
        var attribute = CreateAttribute(id: 1, name: "Cu");

        _attributeRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(attribute);
        _attributeRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductAttribute, bool>>>()))
            .ReturnsAsync(false);
        _attributeRepository.Setup(repo => repo.UpdateAsync(attribute)).Returns(Task.CompletedTask);
        _attributeRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        var request = new UpdateProductAttributeDto
        {
            Id = 1,
            Name = "Moi",
            Description = "Da cap nhat",
            UpdatedBy = 1001
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        attribute.Name.Should().Be("Moi");
        attribute.Description.Should().Be("Da cap nhat");
        _attributeRepository.Verify(repo => repo.UpdateAsync(attribute), Times.Once);
        _attributeRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_MissingAttribute_ReturnsNotFound()
    {
        // Arrange
        _attributeRepository.Setup(repo => repo.GetByIdAsync(404)).ReturnsAsync((ProductAttribute?)null);

        var request = new UpdateProductAttributeDto
        {
            Id = 404,
            Name = "Khong ton tai"
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        _attributeRepository.Verify(repo => repo.UpdateAsync(It.IsAny<ProductAttribute>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_DuplicatedName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var attribute = CreateAttribute(id: 1, name: "Cu");

        _attributeRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(attribute);
        _attributeRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<ProductAttribute, bool>>>()))
            .ReturnsAsync(true);

        var request = new UpdateProductAttributeDto
        {
            Id = 1,
            Name = "Bi trung"
        };

        // Act
        var response = await _sut.UpdateAsync(request);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _attributeRepository.Verify(repo => repo.UpdateAsync(It.IsAny<ProductAttribute>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryDeletes_ReturnsSuccess()
    {
        // Arrange
        _attributeRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(true);
        _attributeRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(true);
        _attributeRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "ProductAttribute")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenRepositoryCannotDelete_ReturnsBadRequest()
    {
        // Arrange
        _attributeRepository.Setup(repo => repo.SoftDeleteAsync(404)).ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(404);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
        _attributeRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    private void SetupAttributes(List<ProductAttribute> attributes)
    {
        _attributeRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<ProductAttribute, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<ProductAttribute, bool>> predicate, bool _) =>
                attributes.AsQueryable().Where(predicate).BuildMock());
    }

    private static ProductAttribute CreateAttribute(
        int id,
        string name,
        string? description = null,
        bool isDeleted = false)
    {
        return new ProductAttribute
        {
            Id = id,
            Name = name,
            Description = description ?? $"{name} description",
            IsDeleted = isDeleted,
            CreatedDate = DateTime.Now
        };
    }
}
