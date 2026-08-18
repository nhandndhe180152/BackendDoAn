using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Locations;

[Trait("Service", "Location")]
public class LocationServiceTests
{
    private readonly Mock<ILocationRepository> _locationRepo = new();
    private readonly Mock<IWarehouseRepository> _warehouseRepo = new();

    private LocationService Sut() => new(_locationRepo.Object, _warehouseRepo.Object);

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        _locationRepo.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<Expression<Func<Location, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Location, object>>[]>()))
            .ReturnsAsync((Location?)null);

        var result = await Sut().GetByIdAsync(1);

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task GetAllAsync_EmptyList_ReturnsSuccess()
    {
        _locationRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Location, bool>>>(),
                It.IsAny<bool>(),
                It.IsAny<Expression<Func<Location, object>>[]>()))
            .Returns(new List<Location>().AsQueryable().BuildMock());

        var result = await Sut().GetAllAsync();

        result.Status.Should().Be(200);
    }
}
