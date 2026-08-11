using Backend.Application.Constants;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MockQueryable.Moq;
using Moq;
using Xunit;
using InventoryEntity = Backend.Domain.Entities.Inventory;

namespace Backend.UnitTest.Services.PaddyLotBags;

[Trait("Service", "PaddyLotBagInvariant")]
public class PaddyLotBagInvariantServiceTests
{
    private static PaddyLotBagInvariantService CreateSut(
        IEnumerable<PaddyLotBag> bags,
        IEnumerable<PaddyLotBagContent> contents,
        IEnumerable<InventoryEntity>? inventories = null)
    {
        var context = new Mock<IApplicationDbContext>();
        context.Setup(x => x.PaddyLotBags)
            .Returns(bags.AsQueryable().BuildMockDbSet().Object);
        context.Setup(x => x.PaddyLotBagContents)
            .Returns(contents.AsQueryable().BuildMockDbSet().Object);
        context.Setup(x => x.Inventories)
            .Returns((inventories ?? Array.Empty<InventoryEntity>()).AsQueryable().BuildMockDbSet().Object);
        return new PaddyLotBagInvariantService(context.Object);
    }

    [Fact]
    public async Task ValidateBagAsync_WhenContentMatchesWithinTolerance_Passes()
    {
        var bag = new PaddyLotBag { Id = 1, WeightKg = 50m, LocationId = 3, Status = PaddyLotBagStatuses.Stored };
        var contents = new[]
        {
            new PaddyLotBagContent { BagId = 1, WeightKg = 20m },
            new PaddyLotBagContent { BagId = 1, WeightKg = 29.9995m }
        };

        Func<Task> action = () => CreateSut(new[] { bag }, contents).ValidateBagAsync(1);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateBagAsync_WhenContentDoesNotMatch_Throws()
    {
        var bag = new PaddyLotBag { Id = 1, WeightKg = 50m, LocationId = 3, Status = PaddyLotBagStatuses.Stored };
        var contents = new[] { new PaddyLotBagContent { BagId = 1, WeightKg = 49m } };

        Func<Task> action = () => CreateSut(new[] { bag }, contents).ValidateBagAsync(1);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*không cân bằng*");
    }

    [Fact]
    public async Task ValidateLotLocationAsync_WhenContentMatchesInventory_Passes()
    {
        var bag = new PaddyLotBag { Id = 1, LotId = 10, WeightKg = 50m, LocationId = 3, Status = PaddyLotBagStatuses.Stored };
        var contents = new[] { new PaddyLotBagContent { BagId = 1, LotId = 10, WeightKg = 50m, Bag = bag } };
        var inventories = new[] { new InventoryEntity { PaddyLotId = 10, LocationId = 3, QuantityOnHand = 50m } };

        Func<Task> action = () => CreateSut(new[] { bag }, contents, inventories).ValidateLotLocationAsync(10, 3);

        await action.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ValidateLotLocationAsync_WhenContentDoesNotMatchInventory_Throws()
    {
        var bag = new PaddyLotBag { Id = 1, LotId = 10, WeightKg = 50m, LocationId = 3, Status = PaddyLotBagStatuses.Stored };
        var contents = new[] { new PaddyLotBagContent { BagId = 1, LotId = 10, WeightKg = 50m, Bag = bag } };
        var inventories = new[] { new InventoryEntity { PaddyLotId = 10, LocationId = 3, QuantityOnHand = 49m } };

        Func<Task> action = () => CreateSut(new[] { bag }, contents, inventories).ValidateLotLocationAsync(10, 3);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*lệch tồn*");
    }
}
