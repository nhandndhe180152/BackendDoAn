using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Application.DTOs.UserSessions;
using Backend.Application.Implements;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.User;

public class UserSessionServiceTests
{
    private readonly Mock<IUserSessionRepository> _sessionRepository = new();
    private readonly UserSessionService _sut;

    public UserSessionServiceTests()
    {
        _sut = new UserSessionService(_sessionRepository.Object);
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateAsync(new CreateUserSessionDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateListAsync(new List<CreateUserSessionDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetAllAsync();
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetByIdAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new SearchQuery());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "GetPagedAdvanced")]
    public async Task GetPagedAsync_Advanced_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new AdvancedSearchQuery<object>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new DTParameter());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteListAsync(new List<int>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateAsync(new UpdateUserSessionDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserSession")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateListAsync(new List<UpdateUserSessionDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }
}
