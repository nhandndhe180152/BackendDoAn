using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Permissions;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Permission;

public class PermissionServiceTests
{
    private readonly Mock<IPermissionRepository> _permissionRepository = new();
    private readonly Mock<IHttpContextAccessor> _httpContextAccessor = new();
    private readonly PermissionService _sut;

    public PermissionServiceTests()
    {
        _sut = new PermissionService(_permissionRepository.Object, _httpContextAccessor.Object);
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            ActionId = 1,
            MenuId = 2,
            RoleId = 3
        };

        _permissionRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Permission, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        response.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
        _permissionRepository.Verify(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Permission>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new CreatePermissionDto
        {
            ActionId = 1,
            MenuId = 2,
            RoleId = 3
        };

        _permissionRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Permission, bool>>>()))
            .ReturnsAsync(false);

        _permissionRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Permission>()))
            .Returns(Task.CompletedTask);

        _permissionRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _permissionRepository.Verify(repo => repo.CreateAsync(It.IsAny<Backend.Domain.Entities.Permission>()), Times.Once);
        _permissionRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _permissionRepository
            .Setup(repo => repo.SoftDeleteAsync(1))
            .ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
        _permissionRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenExists_DeletesAndSaves()
    {
        // Arrange
        _permissionRepository
            .Setup(repo => repo.SoftDeleteAsync(1))
            .ReturnsAsync(true);

        _permissionRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        response.Resources.Should().Be(true);
        _permissionRepository.Verify(repo => repo.SoftDeleteAsync(1), Times.Once);
        _permissionRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateListAsync(new List<CreatePermissionDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetAllAsync();
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetByIdAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new SearchQuery());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "GetPagedAdvanced")]
    public async Task GetPagedAsync_Advanced_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new AdvancedSearchQuery<object>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new DTParameter());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteListAsync(new List<int>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateAsync(new UpdatePermissionDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "Permission")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateListAsync(new List<UpdatePermissionDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }
}
