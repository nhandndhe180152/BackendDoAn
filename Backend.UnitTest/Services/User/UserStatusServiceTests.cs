using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.UserStatuses;
using Backend.Application.Implements;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.User;

public class UserStatusServiceTests
{
    private readonly Mock<IUserStatusRepository> _statusRepository = new();
    private readonly UserStatusService _sut;

    public UserStatusServiceTests()
    {
        _sut = new UserStatusService(_statusRepository.Object);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenNameDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateUserStatusDto { Name = "Active", Color = "#00FF00" };

        _statusRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(true); // Simulate name duplicate exists

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        _statusRepository.Verify(repo => repo.CreateAsync(It.IsAny<UserStatus>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenColorDuplicate_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new CreateUserStatusDto { Name = "Active", Color = "#00FF00" };

        // First call for Name check returns false, second for Color check returns true
        _statusRepository
            .SetupSequence(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
        _statusRepository.Verify(repo => repo.CreateAsync(It.IsAny<UserStatus>()), Times.Never);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_WhenValid_CreatesAndSaves()
    {
        // Arrange
        var dto = new CreateUserStatusDto { Name = "Active", Color = "#00FF00", Description = "Active state" };

        _statusRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(false);

        _statusRepository
            .Setup(repo => repo.CreateAsync(It.IsAny<UserStatus>()))
            .Returns(Task.CompletedTask);

        _statusRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(1);

        // Act
        var response = await _sut.CreateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        _statusRepository.Verify(repo => repo.CreateAsync(It.IsAny<UserStatus>()), Times.Once);
        _statusRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_CreatesAllAndSaves()
    {
        // Arrange
        var dtos = new List<CreateUserStatusDto>
        {
            new() { Name = "Active", Color = "#00FF00" },
            new() { Name = "Inactive", Color = "#FF0000" }
        };

        _statusRepository
            .Setup(repo => repo.CreateListAsync(It.IsAny<IEnumerable<UserStatus>>()))
            .Returns(Task.CompletedTask);

        _statusRepository
            .Setup(repo => repo.SaveChangesAsync())
            .ReturnsAsync(2);

        // Act
        var response = await _sut.CreateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(201);
        _statusRepository.Verify(repo => repo.CreateListAsync(It.IsAny<IEnumerable<UserStatus>>()), Times.Once);
        _statusRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_ReturnsNonDeletedStatuses()
    {
        // Arrange
        var list = new List<UserStatus>
        {
            new() { Id = 1, Name = "Active", Color = "Green", IsDeleted = false },
            new() { Id = 2, Name = "Inactive", Color = "Red", IsDeleted = false },
            new() { Id = 3, Name = "Archived", Color = "Gray", IsDeleted = true }
        };

        SetupStatuses(list);

        // Act
        var response = await _sut.GetAllAsync();

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var data = response.Resources as List<UserStatusListDto>;
        data.Should().NotBeNull();
        data.Should().HaveCount(2);
        data.Any(x => x.Id == 3).Should().BeFalse();
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenNotExists_ReturnsNotFound()
    {
        // Arrange
        SetupStatuses(new List<UserStatus>());

        // Act
        var response = await _sut.GetByIdAsync(99);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_WhenExists_ReturnsStatusDetail()
    {
        // Arrange
        var list = new List<UserStatus>
        {
            new() { Id = 5, Name = "Pending", Color = "Yellow", IsDeleted = false }
        };
        SetupStatuses(list);

        // Act
        var response = await _sut.GetByIdAsync(5);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var detail = response.Resources as UserStatusDetailDto;
        detail.Should().NotBeNull();
        detail!.Id.Should().Be(5);
        detail.Name.Should().Be("Pending");
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_AppliesKeywordFilterAndPaging()
    {
        // Arrange
        var list = new List<UserStatus>
        {
            new() { Id = 1, Name = "Active Status", Description = "User is active", IsDeleted = false },
            new() { Id = 2, Name = "Suspended State", Description = "Account suspended", IsDeleted = false },
            new() { Id = 3, Name = "Deleted Status", Description = "Should be skipped since IsDeleted=true", IsDeleted = true }
        };
        SetupStatuses(list);

        var query = new SearchQuery { PageIndex = 1, PageSize = 1, Keyword = "suspended" };

        // Act
        var response = await _sut.GetPagedAsync(query);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        var pagedData = response.Resources as PagingData<UserStatusListDto>;
        pagedData.Should().NotBeNull();
        pagedData!.Total.Should().Be(2); // Total active statuses is 2
        pagedData.TotalFiltered.Should().Be(1); // Filtered by keyword "suspended"
        pagedData.DataSource.Should().HaveCount(1);
        pagedData.DataSource.First().Name.Should().Be("Suspended State");
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_CallsRepository()
    {
        // Arrange
        var parameters = new DTParameter();
        var dtResult = new DTResult<UserStatusAggregate> { draw = 1 };

        _statusRepository
            .Setup(repo => repo.GetPagedAsync(parameters))
            .ReturnsAsync(dtResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        response.Resources.Should().Be(dtResult);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenSuccess_SavesChanges()
    {
        // Arrange
        _statusRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(true);
        _statusRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        _statusRepository.Verify(repo => repo.SoftDeleteAsync(1), Times.Once);
        _statusRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        _statusRepository.Setup(repo => repo.SoftDeleteAsync(1)).ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteAsync(1);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_WhenSuccess_ReturnsSuccess()
    {
        // Arrange
        var ids = new List<int> { 1, 2 };
        _statusRepository.Setup(repo => repo.SoftDeleteListAsync(ids)).ReturnsAsync(true);

        // Act
        var response = await _sut.SoftDeleteListAsync(ids);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_WhenFailure_ReturnsBadRequest()
    {
        // Arrange
        var ids = new List<int> { 1, 2 };
        _statusRepository.Setup(repo => repo.SoftDeleteListAsync(ids)).ReturnsAsync(false);

        // Act
        var response = await _sut.SoftDeleteListAsync(ids);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDuplicateName_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new UpdateUserStatusDto { Id = 1, Name = "Duplicate", Color = "#00FF00" };

        _statusRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(true);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenDuplicateColor_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = new UpdateUserStatusDto { Id = 1, Name = "UniqueName", Color = "#00FF00" };

        _statusRepository
            .SetupSequence(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(false)
            .ReturnsAsync(true);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new UpdateUserStatusDto { Id = 1, Name = "UniqueName", Color = "#00FF00" };

        _statusRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(false);

        SetupStatuses(new List<UserStatus>()); // Empty list means not found

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_WhenValid_UpdatesAndSaves()
    {
        // Arrange
        var dto = new UpdateUserStatusDto { Id = 1, Name = "UpdatedName", Color = "#111111", Description = "New Desc" };
        var existing = new UserStatus { Id = 1, Name = "OldName", Color = "#222222", IsDeleted = false };

        _statusRepository
            .Setup(repo => repo.AnyAsync(It.IsAny<Expression<Func<UserStatus, bool>>>()))
            .ReturnsAsync(false);

        SetupStatuses(new List<UserStatus> { existing });

        _statusRepository.Setup(repo => repo.UpdateAsync(existing)).Returns(Task.CompletedTask);
        _statusRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var response = await _sut.UpdateAsync(dto);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        existing.Name.Should().Be("UpdatedName");
        existing.Color.Should().Be("#111111");
        _statusRepository.Verify(repo => repo.UpdateAsync(existing), Times.Once);
        _statusRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_WhenCountMismatch_ReturnsBadRequest()
    {
        // Arrange
        var dtos = new List<UpdateUserStatusDto>
        {
            new() { Id = 1, Name = "Name1" },
            new() { Id = 2, Name = "Name2" }
        };

        SetupStatuses(new List<UserStatus> { new() { Id = 1 } }); // Only found 1 of 2

        // Act
        var response = await _sut.UpdateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeFalse();
        response.Status.Should().Be(400);
    }

    [Fact]
    [Trait("Service", "UserStatus")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_WhenValid_UpdatesAllAndSaves()
    {
        // Arrange
        var dtos = new List<UpdateUserStatusDto>
        {
            new() { Id = 1, Name = "New1", Color = "C1" },
            new() { Id = 2, Name = "New2", Color = "C2" }
        };

        var list = new List<UserStatus>
        {
            new() { Id = 1, Name = "Old1", Color = "OldC1" },
            new() { Id = 2, Name = "Old2", Color = "OldC2" }
        };

        SetupStatuses(list);
        _statusRepository.Setup(repo => repo.UpdateListAsync(It.IsAny<IEnumerable<UserStatus>>())).Returns(Task.CompletedTask);
        _statusRepository.Setup(repo => repo.SaveChangesAsync()).ReturnsAsync(2);

        // Act
        var response = await _sut.UpdateListAsync(dtos);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        list[0].Name.Should().Be("New1");
        list[1].Name.Should().Be("New2");
        _statusRepository.Verify(repo => repo.UpdateListAsync(It.IsAny<IEnumerable<UserStatus>>()), Times.Once);
        _statusRepository.Verify(repo => repo.SaveChangesAsync(), Times.Once);
    }

    private void SetupStatuses(List<UserStatus> statuses)
    {
        _statusRepository
            .Setup(repo => repo.FindByCondition(
                It.IsAny<Expression<Func<UserStatus, bool>>>(),
                It.IsAny<bool>()
            ))
            .Returns((Expression<Func<UserStatus, bool>> predicate, bool _) =>
                statuses.AsQueryable().Where(predicate).BuildMock());
    }
}
