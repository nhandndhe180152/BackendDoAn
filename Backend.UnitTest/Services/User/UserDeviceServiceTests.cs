using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.DTOs.UserDevices;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.User;

public class UserDeviceServiceTests
{
    private readonly Mock<IUserDeviceRepository> _deviceRepository = new();
    private readonly Mock<IUserSessionRepository> _sessionRepository = new();
    private readonly UserDeviceService _sut;

    public UserDeviceServiceTests()
    {
        _sut = new UserDeviceService(_deviceRepository.Object, _sessionRepository.Object);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsSuccess()
    {
        // Arrange
        var parameters = new DTParameter();
        var mockResult = new DTResult<UserDeviceAggregate>
        {
            draw = 1,
            recordsTotal = 5,
            recordsFiltered = 5,
            data = new List<UserDeviceAggregate>()
        };

        _deviceRepository
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
    [Trait("Service", "UserDevice")]
    [Trait("Method", "AddDeviceToken")]
    public async Task AddDeviceToken_WhenTokenNotExists_CreatesNewToken()
    {
        // Arrange
        var dto = new CreateUserDeviceDto
        {
            UserId = 1,
            DeviceToken = "new-token",
            Platform = "Android"
        };

        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ReturnsAsync((UserDevice?)null);

        _deviceRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<UserDevice>()))
            .Returns(Task.CompletedTask);

        _deviceRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.AddDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _deviceRepository.Verify(repo => repo.CreateAsync(It.Is<UserDevice>(ud => ud.UserId == dto.UserId && ud.DeviceToken == dto.DeviceToken)), Times.Once);
        _deviceRepository.Verify(repo => repo.UpdateAsync(It.IsAny<UserDevice>()), Times.Never);
        _deviceRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "AddDeviceToken")]
    public async Task AddDeviceToken_WhenTokenExists_UpdatesIsDeletedToFalse()
    {
        // Arrange
        var dto = new CreateUserDeviceDto
        {
            UserId = 1,
            DeviceToken = "existing-token",
            Platform = "Android"
        };

        var existing = new UserDevice
        {
            Id = 10,
            UserId = 1,
            DeviceToken = "existing-token",
            IsDeleted = true
        };

        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ReturnsAsync(existing);

        _deviceRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<UserDevice>()))
            .Returns(Task.CompletedTask);

        _deviceRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.AddDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        existing.IsDeleted.Should().BeFalse();
        _deviceRepository.Verify(repo => repo.CreateAsync(It.IsAny<UserDevice>()), Times.Never);
        _deviceRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        _deviceRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "AddDeviceToken")]
    public async Task AddDeviceToken_WhenExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var dto = new CreateUserDeviceDto();
        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var response = await _sut.AddDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(500);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "DeleteDeviceToken")]
    public async Task DeleteDeviceToken_WhenTokenExists_UpdatesIsDeletedToTrue()
    {
        // Arrange
        var dto = new DeleteUserDeviceDto
        {
            UserId = 1,
            DeviceToken = "active-token"
        };

        var existing = new UserDevice
        {
            Id = 10,
            UserId = 1,
            DeviceToken = "active-token",
            IsDeleted = false
        };

        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ReturnsAsync(existing);

        _deviceRepository
            .Setup(repo => repo.UpdateAsync(It.IsAny<UserDevice>()))
            .Returns(Task.CompletedTask);

        _deviceRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.DeleteDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        existing.IsDeleted.Should().BeTrue();
        _deviceRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        _deviceRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "DeleteDeviceToken")]
    public async Task DeleteDeviceToken_WhenTokenNotExists_DoesNothingReturnsSuccess()
    {
        // Arrange
        var dto = new DeleteUserDeviceDto
        {
            UserId = 1,
            DeviceToken = "not-found-token"
        };

        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ReturnsAsync((UserDevice?)null);

        // Act
        var response = await _sut.DeleteDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _deviceRepository.Verify(repo => repo.UpdateAsync(It.IsAny<UserDevice>()), Times.Never);
        _deviceRepository.Verify(repo => repo.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "DeleteDeviceToken")]
    public async Task DeleteDeviceToken_WhenExceptionThrown_ReturnsInternalServerError()
    {
        // Arrange
        var dto = new DeleteUserDeviceDto();
        _deviceRepository
            .Setup(repo => repo.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<UserDevice, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<UserDevice, object>>[]>()
            ))
            .ThrowsAsync(new Exception("Database connection failed"));

        // Act
        var response = await _sut.DeleteDeviceToken(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(500);
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateAsync(new CreateUserDeviceDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateListAsync(new List<CreateUserDeviceDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetAllAsync();
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetByIdAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new SearchQuery());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "GetPagedAdvanced")]
    public async Task GetPagedAsync_Advanced_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new AdvancedSearchQuery<object>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteListAsync(new List<int>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateAsync(new UpdateUserDeviceDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserDevice")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateListAsync(new List<UpdateUserDeviceDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }
}
