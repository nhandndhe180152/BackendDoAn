using System.Linq.Expressions;
using Backend.Application.DTOs.Actions;
using Backend.Application.Implements;
using Backend.Domain.Aggregates;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using MockQueryable.Moq;
using Moq;

using ActionEntity = Backend.Domain.Entities.Action;

namespace Backend.UnitTest.Services.Action;

[Trait("Service", "Action")]
public class ActionServiceTests
{
    private readonly Mock<IActionRepository> _repo = new();

    private ActionService Sut() => new(_repo.Object);

    [Fact]
    public async Task CreateAsync_ValidDto_CreatesAndSaves()
    {
        _repo.Setup(x => x.AnyAsync(It.IsAny<Expression<Func<ActionEntity, bool>>>()))
            .ReturnsAsync(false);

        var result = await Sut().CreateAsync(new CreateActionDto
        {
            Code = "CREATE_USER",
            Name = "Create user",
            Description = "Allows creating users"
        });

        result.Status.Should().Be(201);
        _repo.Verify(x => x.CreateAsync(It.Is<ActionEntity>(a =>
            a.Code == "CREATE_USER" && a.Name == "Create user")), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GetByIdAsync_ExistingAction_ReturnsSuccess()
    {
        _repo.Setup(x => x.GetByIdAsync(7))
            .ReturnsAsync(new ActionEntity { Id = 7, Code = "VIEW", Name = "View" });

        var result = await Sut().GetByIdAsync(7);

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task GetPagedAsync_ValidQuery_ReturnsSuccess()
    {
        SetupQueryable(
            new ActionEntity { Id = 1, Name = "Create user", Description = "User action" },
            new ActionEntity { Id = 2, Name = "View report", Description = "Report action" });

        var result = await Sut().GetPagedAsync(new SearchQuery
        {
            PageIndex = 1,
            PageSize = 10
        });

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task GetPagedAsync_WithKeyword_AppliesKeywordBranch()
    {
        SetupQueryable(
            new ActionEntity { Id = 1, Name = "Create user", Description = "User action" },
            new ActionEntity { Id = 2, Name = "View report", Description = "Report action" });

        var result = await Sut().GetPagedAsync(new SearchQuery
        {
            Keyword = "report",
            PageIndex = 1,
            PageSize = 10
        });

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task GetPagedAsync_WithOrderBy_AppliesOrderingBranch()
    {
        SetupQueryable(
            new ActionEntity { Id = 1, Name = "Beta" },
            new ActionEntity { Id = 2, Name = "Alpha" });

        var result = await Sut().GetPagedAsync(new SearchQuery
        {
            OrderBy = "Name",
            SortType = "asc",
            PageIndex = 1,
            PageSize = 10
        });

        result.Status.Should().Be(200);
    }

    [Fact]
    public async Task SoftDeleteAsync_ExistingAction_DeletesAndSaves()
    {
        _repo.Setup(x => x.SoftDeleteAsync(5)).ReturnsAsync(true);

        var result = await Sut().SoftDeleteAsync(5);

        result.Status.Should().Be(200);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_ValidDto_UpdatesAndSaves()
    {
        var entity = new ActionEntity { Id = 3, Code = "OLD", Name = "Old" };
        _repo.Setup(x => x.AnyAsync(It.IsAny<Expression<Func<ActionEntity, bool>>>()))
            .ReturnsAsync(false);
        _repo.Setup(x => x.GetByIdAsync(3)).ReturnsAsync(entity);

        var result = await Sut().UpdateAsync(new UpdateActionDto
        {
            Id = 3,
            Code = "NEW",
            Name = "New"
        });

        result.Status.Should().Be(200);
        entity.Code.Should().Be("NEW");
        entity.Name.Should().Be("New");
        _repo.Verify(x => x.UpdateAsync(entity), Times.Once);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateName_Returns422WithoutWriting()
    {
        _repo.Setup(x => x.AnyAsync(It.IsAny<Expression<Func<ActionEntity, bool>>>()))
            .ReturnsAsync(true);

        var result = await Sut().UpdateAsync(new UpdateActionDto { Id = 3, Name = "Duplicate" });

        result.Status.Should().Be(422);
        _repo.Verify(x => x.UpdateAsync(It.IsAny<ActionEntity>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateAsync_ActionNotFound_Returns404WithoutWriting()
    {
        _repo.Setup(x => x.AnyAsync(It.IsAny<Expression<Func<ActionEntity, bool>>>()))
            .ReturnsAsync(false);
        _repo.Setup(x => x.GetByIdAsync(999)).ReturnsAsync((ActionEntity?)null);

        var result = await Sut().UpdateAsync(new UpdateActionDto { Id = 999, Name = "Missing" });

        result.Status.Should().Be(404);
        _repo.Verify(x => x.UpdateAsync(It.IsAny<ActionEntity>()), Times.Never);
        _repo.Verify(x => x.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task GetPagedAsync_DtParameters_ReturnsSuccess()
    {
        var parameters = new DTParameter();
        _repo.Setup(x => x.GetPagedAsync(parameters)).ReturnsAsync(new DTResult<ActionAggregate>());

        var result = await Sut().GetPagedAsync(parameters);

        result.Status.Should().Be(200);
        _repo.Verify(x => x.GetPagedAsync(parameters), Times.Once);
    }

    private void SetupQueryable(params ActionEntity[] actions)
    {
        _repo.Setup(x => x.FindByCondition(
                It.IsAny<Expression<Func<ActionEntity, bool>>>(), It.IsAny<bool>()))
            .Returns(actions.AsQueryable().BuildMock());
    }
}
