using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDevices;
using Backend.Application.Implements;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.IoT;

public class IotDeviceServiceTests
{
    private readonly Mock<IRepositoryBase<IotDevice, int>> _iotDeviceBaseRepository = new();
    private readonly Mock<IRepositoryBase<Backend.Domain.Entities.Warehouse, int>> _warehouseRepository = new();
    private readonly Mock<IIotDeviceRepository> _iotDeviceRepository = new();
    private readonly IotDeviceService _sut;

    public IotDeviceServiceTests()
    {
        _sut = new IotDeviceService(
            _iotDeviceBaseRepository.Object,
            _warehouseRepository.Object,
            _iotDeviceRepository.Object
        );
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenWarehouseNotExists_ReturnsNotFound()
    {
        // Arrange
        var dto = new CreateIotDeviceDto { WarehouseId = 1, DeviceCode = "DEV01" };
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(false); // Warehouse not found or inactive

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenDeviceCodeDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateIotDeviceDto { WarehouseId = 1, DeviceCode = "DEV01" };
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);

        _iotDeviceBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDevice, bool>>>()))
            .ReturnsAsync(true); // Duplicate code

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new CreateIotDeviceDto { WarehouseId = 1, DeviceCode = "DEV01", ApiKey = "customkey" };
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);

        _iotDeviceBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDevice, bool>>>()))
            .ReturnsAsync(false);

        _iotDeviceBaseRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<IotDevice>()))
            .Returns(Task.CompletedTask);

        _iotDeviceBaseRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        var result = response.Resources as CreateIotDeviceResultDto;
        result.Should().NotBeNull();
        result!.DeviceCode.Should().Be("DEV01");
        result.ApiKey.Should().Be("customkey");
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_ReturnsMappedDevices()
    {
        // Arrange
        var warehouse = new Backend.Domain.Entities.Warehouse { Id = 1, Code = "WH1", Name = "WH 1" };
        var list = new List<IotDevice>
        {
            new() { Id = 1, DeviceCode = "DEV1", DeviceName = "Device 1", WarehouseId = 1, Warehouse = warehouse }
        };

        _iotDeviceBaseRepository
            .Setup(repo => repo.GetAll(It.IsAny<bool>(), It.IsAny<Expression<Func<IotDevice, object>>[]>()))
            .Returns(list.AsQueryable().BuildMock());

        // Act
        var response = await _sut.GetAllAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var resources = response.Resources as List<IotDeviceDetailDto>;
        resources.Should().NotBeNull();
        resources.Should().HaveCount(1);
        resources!.First().DeviceCode.Should().Be("DEV1");
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        _iotDeviceRepository.Setup(repo => repo.GetDetailByIdAsync(1)).ReturnsAsync((IotDeviceAggregate?)null);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenExists_ReturnsDeviceDetail()
    {
        // Arrange
        var agg = new IotDeviceAggregate { Id = 1, DeviceCode = "DEV1" };
        _iotDeviceRepository.Setup(repo => repo.GetDetailByIdAsync(1)).ReturnsAsync(agg);

        // Act
        var response = await _sut.GetByIdAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        var res = response.Resources as IotDeviceDetailDto;
        res.Should().NotBeNull();
        res!.DeviceCode.Should().Be("DEV1");
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsResult()
    {
        // Arrange
        var parameters = new DTParameter();
        var mockResult = new DTResult<IotDeviceAggregate> { draw = 1 };
        _iotDeviceRepository.Setup(repo => repo.GetPagedAsync(parameters)).ReturnsAsync(mockResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Resources.Should().Be(mockResult);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDeviceNotExists_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateIotDeviceDto { Id = 1 };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync((IotDevice?)null);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenWarehouseNotExists_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateIotDeviceDto { Id = 1, WarehouseId = 2 };
        var existing = new IotDevice { Id = 1 };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(false);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDeviceCodeDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new UpdateIotDeviceDto { Id = 1, WarehouseId = 2, DeviceCode = "DUP" };
        var existing = new IotDevice { Id = 1 };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);

        _iotDeviceBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDevice, bool>>>()))
            .ReturnsAsync(true); // duplicate code

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenValid_UpdatesAndSaves()
    {
        // Arrange
        var dto = new UpdateIotDeviceDto { Id = 1, WarehouseId = 2, DeviceCode = "NEW-CODE", DeviceName = "NEW-NAME" };
        var existing = new IotDevice { Id = 1, DeviceCode = "OLD-CODE", DeviceName = "OLD-NAME" };

        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _warehouseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<Backend.Domain.Entities.Warehouse, bool>>>()))
            .ReturnsAsync(true);
        _iotDeviceBaseRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<IotDevice, bool>>>()))
            .ReturnsAsync(false);

        _iotDeviceBaseRepository.Setup(repo => repo.UpdateAsync(existing)).Returns(Task.CompletedTask);
        _iotDeviceBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        existing.DeviceCode.Should().Be("NEW-CODE");
        existing.DeviceName.Should().Be("NEW-NAME");
        _iotDeviceBaseRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        _iotDeviceBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "UpdateActiveStatus")]
    public async Task UpdateActiveStatusAsync_WhenExists_UpdatesAndSaves()
    {
        // Arrange
        var existing = new IotDevice { Id = 1, IsActive = false };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _iotDeviceBaseRepository.Setup(repo => repo.UpdateAsync(existing)).Returns(Task.CompletedTask);
        _iotDeviceBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.UpdateActiveStatusAsync(1, true, 100);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        existing.IsActive.Should().BeTrue();
        existing.UpdatedBy.Should().Be(100);
        _iotDeviceBaseRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "RegenerateApiKey")]
    public async Task RegenerateApiKeyAsync_WhenExists_RegeneratesAndSaves()
    {
        // Arrange
        var existing = new IotDevice { Id = 1, DeviceCode = "DEV1", ApiKeyHash = "old_hash" };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _iotDeviceBaseRepository.Setup(repo => repo.UpdateAsync(existing)).Returns(Task.CompletedTask);
        _iotDeviceBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.RegenerateApiKeyAsync(1, 200);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        existing.ApiKeyHash.Should().NotBe("old_hash");
        existing.UpdatedBy.Should().Be(200);
        _iotDeviceBaseRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    [Trait("Service", "IotDevice")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenExists_DeletesAndSaves()
    {
        // Arrange
        var existing = new IotDevice { Id = 1 };
        _iotDeviceBaseRepository.Setup(repo => repo.GetByIdAsync(1)).ReturnsAsync(existing);
        _iotDeviceBaseRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(true);
        _iotDeviceBaseRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        _iotDeviceBaseRepository.Verify(repo => repo.SoftDeleteAsync(1), Times.Once);
        _iotDeviceBaseRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }
}
