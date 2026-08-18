using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.Alerts;

[Trait("Service", "Alert")]
public class AlertServiceTests
{
    private readonly Mock<IAlertRepository> _alertRepo = new();
    private readonly Mock<ISystemConfigService> _sysConfig = new();

    private AlertService Sut() => new(_alertRepo.Object, _sysConfig.Object);

    [Fact]
    public async Task AcknowledgeAsync_NotFound_Returns404()
    {
        _alertRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync((Alert?)null);

        var result = await Sut().AcknowledgeAsync(1, 1);

        result.Status.Should().Be(404);
    }

    [Fact]
    public async Task AcknowledgeAsync_AlreadyResolved_Returns400()
    {
        _alertRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(new Alert { Id = 1, Status = AlertConstants.Status.Resolved });

        var result = await Sut().AcknowledgeAsync(1, 1);

        result.Status.Should().Be(400);
    }

    [Fact]
    public async Task AcknowledgeAsync_OpenAlert_SetsAcknowledged()
    {
        var alert = new Alert { Id = 1, Status = AlertConstants.Status.Open };
        _alertRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(alert);

        var result = await Sut().AcknowledgeAsync(1, 7);

        result.Status.Should().Be(200);
        alert.Status.Should().Be(AlertConstants.Status.Acknowledged);
        alert.AcknowledgedBy.Should().Be(7);
    }

    [Fact]
    public async Task ResolveAsync_OpenAlert_SetsResolved()
    {
        var alert = new Alert { Id = 1, Status = AlertConstants.Status.Open };
        _alertRepo.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(alert);

        var result = await Sut().ResolveAsync(1, 7);

        result.Status.Should().Be(200);
        alert.Status.Should().Be(AlertConstants.Status.Resolved);
    }

    [Fact]
    public async Task ResolveAsync_NotFound_Returns404()
    {
        _alertRepo.Setup(r => r.GetByIdAsync(5)).ReturnsAsync((Alert?)null);

        var result = await Sut().ResolveAsync(5, 1);

        result.Status.Should().Be(404);
    }
}
