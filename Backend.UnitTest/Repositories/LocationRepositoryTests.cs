using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Backend.Infrastructure.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Backend.UnitTest.Repositories;

[Trait("Repository", "Location")]
public class LocationRepositoryTests
{
    private static BackendContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<BackendContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new BackendContext(options);
    }

    [Fact]
    public async Task TryLockForOutboundAsync_ActiveLockFromAnotherOrder_IsRejected()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 14, 10, 0, 0);
        var location = new Location
        {
            Id = 1,
            WarehouseId = 1,
            ZoneName = "A",
            IsActive = true,
            OutboundLockOrderId = 10,
            OutboundLockedAt = now - OutboundOrderConstants.ColumnLockTimeout + TimeSpan.FromMinutes(1)
        };
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var repository = new LocationRepository(context, Mock.Of<IUnitOfWork>());

        var affected = await repository.TryLockForOutboundAsync(new[] { location.Id }, 20, now, 99);

        affected.Should().Be(0);
        location.OutboundLockOrderId.Should().Be(10);
    }

    [Fact]
    public async Task TryLockForOutboundAsync_ExpiredLockFromAnotherOrder_IsTakenOver()
    {
        await using var context = CreateContext();
        var now = new DateTime(2026, 8, 14, 10, 0, 0);
        var location = new Location
        {
            Id = 1,
            WarehouseId = 1,
            ZoneName = "A",
            IsActive = true,
            OutboundLockOrderId = 10,
            OutboundLockedAt = now - OutboundOrderConstants.ColumnLockTimeout
        };
        context.Locations.Add(location);
        await context.SaveChangesAsync();
        var repository = new LocationRepository(context, Mock.Of<IUnitOfWork>());

        var affected = await repository.TryLockForOutboundAsync(new[] { location.Id }, 20, now, 99);

        affected.Should().Be(1);
        location.OutboundLockOrderId.Should().Be(20);
        location.OutboundLockedAt.Should().Be(now);
        location.UpdatedBy.Should().Be(99);
    }
}
