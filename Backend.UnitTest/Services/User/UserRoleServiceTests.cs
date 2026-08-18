using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Application.DTOs.UserRoles;
using Backend.Application.Implements;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.User;

public class UserRoleServiceTests
{
    private readonly Mock<IUserRoleRepository> _userRoleRepository = new();
    private readonly UserRoleService _sut;

    public UserRoleServiceTests()
    {
        _sut = new UserRoleService(_userRoleRepository.Object);
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateAsync(new CreateUserRoleDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateListAsync(new List<CreateUserRoleDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetAllAsync();
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetByIdAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new SearchQuery());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "GetPagedAdvanced")]
    public async Task GetPagedAsync_Advanced_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new AdvancedSearchQuery<object>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new DTParameter());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteListAsync(new List<int>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateAsync(new UpdateUserRoleDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserRole")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateListAsync(new List<UpdateUserRoleDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }
}
