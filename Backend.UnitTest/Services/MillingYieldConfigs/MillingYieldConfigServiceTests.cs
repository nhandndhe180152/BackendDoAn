using System.Threading.Tasks;
using System.Linq.Expressions;
using System;
using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.MillingYieldConfigs;

[Trait("Service", "MillingYieldConfig")]
public class MillingYieldConfigServiceTests
{
    private readonly Mock<IMillingYieldConfigRepository> _configRepo = new();
    private readonly Mock<IRiceVarietyRepository> _riceVarietyRepo = new();

    private MillingYieldConfigService Sut() => new(_configRepo.Object, _riceVarietyRepo.Object);

    [Fact]
    public async Task CreateAsync_RiceVarietyNotFound_Returns404()
    {
        _riceVarietyRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<global::Backend.Domain.Entities.RiceVariety, bool>>>()))
            .ReturnsAsync(false);

        var result = await Sut().CreateAsync(new CreateMillingYieldConfigDto { RiceVarietyId = 99 });

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task CreateAsync_Duplicate_ReturnsUnprocessable()
    {
        _configRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<MillingYieldConfig, bool>>>()))
            .ReturnsAsync(true);

        var result = await Sut().CreateAsync(new CreateMillingYieldConfigDto { RiceVarietyId = null });

        result.Status.Should().Be(422);
    }

    [Fact]
    public async Task CreateAsync_Valid_ReturnsCreated()
    {
        _configRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<MillingYieldConfig, bool>>>()))
            .ReturnsAsync(false);
        _configRepo.Setup(r => r.CreateAsync(It.IsAny<MillingYieldConfig>())).Returns(Task.CompletedTask);
        _configRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        var result = await Sut().CreateAsync(new CreateMillingYieldConfigDto { RiceVarietyId = null });

        result.Status.Should().Be(201);
    }
}
