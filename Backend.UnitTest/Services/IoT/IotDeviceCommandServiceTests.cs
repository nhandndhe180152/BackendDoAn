using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using Backend.Application.Implements;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.IoT;

public class IotDeviceCommandServiceTests
{
    private readonly Mock<IRepositoryBase<IotDeviceCommand, int>> _commandBaseRepository = new();
    private readonly Mock<IRepositoryBase<IotDevice, int>> _deviceBaseRepository = new();
    private readonly Mock<IIotDeviceCommandRepository> _commandRepository = new();
    private readonly IotDeviceCommandService _sut;

    public IotDeviceCommandServiceTests()
    {
        _sut = new IotDeviceCommandService(
            _commandBaseRepository.Object,
            _deviceBaseRepository.Object,
            _commandRepository.Object
        );
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenDeviceNotExists_ReturnsNotFound()
    {
        // Arrange
        var dto = new CreateIotDeviceCommandDto { IotDeviceId = 1, CommandType = "TARE" };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync((IotDevice?)null);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenCommandTypeNotAllowed_ReturnsBadRequest()
    {
        // Arrange
        var dto = new CreateIotDeviceCommandDto { IotDeviceId = 1, CommandType = "INVALID" };
        var device = new IotDevice { Id = 1, IsActive = true };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenHasPendingCommand_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateIotDeviceCommandDto { IotDeviceId = 1, CommandType = "TARE" };
        var device = new IotDevice { Id = 1, IsActive = true };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        _commandBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDeviceCommand, bool>>>()))
            .ReturnsAsync(true); // Has pending command

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new CreateIotDeviceCommandDto { IotDeviceId = 1, CommandType = "TARE" };
        var device = new IotDevice { Id = 1, IsActive = true };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        _commandBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDeviceCommand, bool>>>()))
            .ReturnsAsync(false);

        _commandBaseRepository
            .Setup(repo => repo.CountByConditionAsync(It.IsAny<Expression<Func<IotDeviceCommand, bool>>>()))
            .ReturnsAsync(0);

        _commandBaseRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<IotDeviceCommand>()))
            .Returns(Task.CompletedTask);

        _commandBaseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        _commandBaseRepository.Verify(repo => repo.CreateAsync(It.IsAny<IotDeviceCommand>()), Times.Once);
        _commandBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _commandRepository.Setup(repo => repo.GetDetailByIdAsync(1)).ReturnsAsync((IotDeviceCommandAggregate?)null);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Cancel")]
    public async Task CancelAsync_WhenNotPending_ReturnsBadRequest()
    {
        // Arrange
        var entity = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.Executed };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(entity);

        // Act
        var response = await _sut.CancelAsync(1, new CancelIotDeviceCommandDto { Reason = "No longer needed" });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "GetPendingCommandForDevice")]
    public async Task GetPendingCommandForDeviceAsync_WhenNoCommand_UpdatesHeartbeatAndReturnsNull()
    {
        // Arrange
        var device = new IotDevice { Id = 1, DeviceCode = "DEV01", ApiKeyHash = null };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        _commandRepository
            .Setup(repo => repo.GetNextPendingCommandAsync(1, true))
            .ReturnsAsync((IotDeviceCommand?)null);

        _deviceBaseRepository
            .Setup(repo => repo.UpdateAsync(device))
            .Returns(Task.CompletedTask);

        _deviceBaseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.GetPendingCommandForDeviceAsync("DEV01", null);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().BeNull();
        device.IsOnline.Should().BeTrue();
        _deviceBaseRepository.Verify(repo => repo.UpdateAsync(device), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "CompleteCommandFromDevice")]
    public async Task CompleteCommandFromDeviceAsync_WhenSuccessful_ExecutesAndSaves()
    {
        // Arrange
        var device = new IotDevice { Id = 1, IsActive = true, IsDeleted = false };
        var command = new IotDeviceCommand { Id = 10, IoTDeviceId = 1, Status = IotDeviceCommandConstants.Status.PickedUp, IotDevice = device };

        _commandRepository
            .Setup(repo => repo.GetCommandWithDeviceAsync(10, true))
            .ReturnsAsync(command);

        _commandBaseRepository
            .Setup(repo => repo.UpdateAsync(command))
            .Returns(Task.CompletedTask);

        _deviceBaseRepository
            .Setup(repo => repo.UpdateAsync(device))
            .Returns(Task.CompletedTask);

        _commandBaseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(2);

        // Act
        var response = await _sut.CompleteCommandFromDeviceAsync(10, new CompleteIotDeviceCommandDto { Success = true, ResultMessage = "Ok" }, null);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        command.Status.Should().Be(IotDeviceCommandConstants.Status.Executed);
        command.ResultMessage.Should().Be("Ok");
        device.IsOnline.Should().BeTrue();
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "GetPendingCommandForDevice")]
    public async Task GetPendingCommandForDeviceAsync_WhenCommandExists_UpdatesStatusAndReturnsCommand()
    {
        // Arrange
        var device = new IotDevice { Id = 1, DeviceCode = "DEV01", ApiKeyHash = null };
        var command = new IotDeviceCommand { Id = 10, IoTDeviceId = 1, Status = IotDeviceCommandConstants.Status.Pending, RetryCount = 0 };

        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        _commandRepository
            .Setup(repo => repo.GetNextPendingCommandAsync(1, true))
            .ReturnsAsync(command);

        _commandBaseRepository.Setup(repo => repo.UpdateAsync(command)).Returns(Task.CompletedTask);
        _deviceBaseRepository.Setup(repo => repo.UpdateAsync(device)).Returns(Task.CompletedTask);
        _commandBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.GetPendingCommandForDeviceAsync("DEV01", null);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().NotBeNull();
        command.Status.Should().Be(IotDeviceCommandConstants.Status.PickedUp);
        command.RetryCount.Should().Be(1);
        device.IsOnline.Should().BeTrue();
        _commandBaseRepository.Verify(repo => repo.UpdateAsync(command), Times.Once);
        _deviceBaseRepository.Verify(repo => repo.UpdateAsync(device), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "CompleteCommandFromDevice")]
    public async Task CompleteCommandFromDeviceAsync_WhenFailed_UpdatesStatusToFailed()
    {
        // Arrange
        var device = new IotDevice { Id = 1, IsActive = true, IsDeleted = false };
        var command = new IotDeviceCommand { Id = 10, IoTDeviceId = 1, Status = IotDeviceCommandConstants.Status.PickedUp, IotDevice = device };

        _commandRepository
            .Setup(repo => repo.GetCommandWithDeviceAsync(10, true))
            .ReturnsAsync(command);

        _commandBaseRepository.Setup(repo => repo.UpdateAsync(command)).Returns(Task.CompletedTask);
        _deviceBaseRepository.Setup(repo => repo.UpdateAsync(device)).Returns(Task.CompletedTask);
        _commandBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var response = await _sut.CompleteCommandFromDeviceAsync(10, new CompleteIotDeviceCommandDto { Success = false, ResultMessage = "Device calibration failed" }, null);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        command.Status.Should().Be(IotDeviceCommandConstants.Status.Failed);
        command.ResultMessage.Should().Be("Device calibration failed");
        device.IsOnline.Should().BeTrue();
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "MarkExpiredCommands")]
    public async Task MarkExpiredCommandsAsync_WhenExpiredCommandsExist_UpdatesStatusAndReturnsCount()
    {
        // Arrange
        var expiredList = new List<IotDeviceCommand>
        {
            new() { Id = 1, Status = IotDeviceCommandConstants.Status.Pending, ExpiredAt = DateTime.Now.AddMinutes(-5) },
            new() { Id = 2, Status = IotDeviceCommandConstants.Status.Pending, ExpiredAt = DateTime.Now.AddMinutes(-10) }
        };

        _commandBaseRepository
            .Setup(repo => repo.FindByCondition(It.IsAny<Expression<Func<IotDeviceCommand, bool>>>(), It.IsAny<bool>()))
            .Returns(expiredList.AsQueryable().BuildMock());

        _commandBaseRepository.Setup(repo => repo.UpdateListAsync(expiredList)).Returns(Task.CompletedTask);
        _commandBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var response = await _sut.MarkExpiredCommandsAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(2);
        expiredList.All(x => x.Status == IotDeviceCommandConstants.Status.Expired).Should().BeTrue();
        _commandBaseRepository.Verify(repo => repo.UpdateListAsync(expiredList), Times.Once);
        _commandBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "MarkExpiredCommands")]
    public async Task MarkExpiredCommandsAsync_WhenNoExpiredCommands_ReturnsZero()
    {
        // Arrange
        var expiredList = new List<IotDeviceCommand>();

        _commandBaseRepository
            .Setup(repo => repo.FindByCondition(It.IsAny<Expression<Func<IotDeviceCommand, bool>>>(), It.IsAny<bool>()))
            .Returns(expiredList.AsQueryable().BuildMock());

        // Act
        var response = await _sut.MarkExpiredCommandsAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(0);
        _commandBaseRepository.Verify(repo => repo.UpdateListAsync(It.IsAny<IEnumerable<IotDeviceCommand>>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_ReturnsActiveCommands()
    {
        // Arrange
        var device = new IotDevice { Id = 1, DeviceCode = "DEV01", DeviceName = "Device 1" };
        var list = new List<IotDeviceCommand>
        {
            new() { Id = 1, IoTDeviceId = 1, IotDevice = device, CommandCode = "CMD01", CommandType = "TARE", Status = "Pending", CreatedDate = DateTime.Now }
        };

        _commandBaseRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<IotDeviceCommand, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<IotDeviceCommand, object>>[]>()))
            .Returns(list.AsQueryable().BuildMock());

        // Act
        var response = await _sut.GetAllAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var data = response.Resources as List<IotDeviceCommandDetailDto>;
        data.Should().NotBeNull();
        data.Should().HaveCount(1);
        data[0].CommandCode.Should().Be("CMD01");
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenCommandNotFound_ReturnsNotFound()
    {
        // Arrange
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync((IotDeviceCommand?)null);

        // Act
        var response = await _sut.UpdateAsync(new UpdateIotDeviceCommandDto { Id = 1 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenCommandNotPending_ReturnsBadRequest()
    {
        // Arrange
        var command = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.Executed };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(command);

        // Act
        var response = await _sut.UpdateAsync(new UpdateIotDeviceCommandDto { Id = 1 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDeviceNotFound_ReturnsNotFound()
    {
        // Arrange
        var command = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.Pending };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(command);
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync((IotDevice?)null);

        // Act
        var response = await _sut.UpdateAsync(new UpdateIotDeviceCommandDto { Id = 1, IotDeviceId = 2 });

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenValid_UpdatesAndSaves()
    {
        // Arrange
        var command = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.Pending, IoTDeviceId = 1 };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(command);

        var device = new IotDevice { Id = 2, IsActive = true };
        _deviceBaseRepository
            .Setup(repo => repo.FirstOrDefaultAsync(It.IsAny<Expression<Func<IotDevice, bool>>>(), It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .ReturnsAsync(device);

        _commandBaseRepository.Setup(repo => repo.UpdateAsync(command)).Returns(Task.CompletedTask);
        _commandBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.UpdateAsync(new UpdateIotDeviceCommandDto { Id = 1, IotDeviceId = 2, CommandType = "RESET" });

        // Assert
        response.IsSucceeded.Should().BeTrue();
        command.IoTDeviceId.Should().Be(2);
        _commandBaseRepository.Verify(repo => repo.UpdateAsync(command), Times.Once);
        _commandBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenCommandNotFound_ReturnsNotFound()
    {
        // Arrange
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync((IotDeviceCommand?)null);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenPickedUp_ReturnsBadRequest()
    {
        // Arrange
        var command = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.PickedUp };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(command);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "IotDeviceCommand")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenSucceeds_SavesChanges()
    {
        // Arrange
        var command = new IotDeviceCommand { Id = 1, Status = IotDeviceCommandConstants.Status.Pending };
        _commandBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(command);
        _commandBaseRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(true);
        _commandBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        _commandBaseRepository.Verify(repo => repo.SoftDeleteAsync(1), Times.Once);
        _commandBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }
}
