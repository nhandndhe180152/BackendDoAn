using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Application.DTOs.UserVerificationTokens;
using Backend.Application.Implements;
using Backend.Domain.Interfaces.Repositories;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;
using FluentAssertions;
using Moq;
using Xunit;

namespace Backend.UnitTest.Services.User;

public class UserVerificationTokenServiceTests
{
    private readonly Mock<IUserVerificationTokenRepository> _tokenRepository = new();
    private readonly UserVerificationTokenService _sut;

    public UserVerificationTokenServiceTests()
    {
        _sut = new UserVerificationTokenService(_tokenRepository.Object);
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "GetPagedDTParameter")]
    public async Task GetPagedAsync_DTParameter_ReturnsSuccess()
    {
        // Arrange
        var parameters = new DTParameter();
        var mockResult = new DTResult<UserVerificationTokenAggregate>
        {
            draw = 1,
            recordsTotal = 10,
            recordsFiltered = 10,
            data = new List<UserVerificationTokenAggregate>()
        };

        _tokenRepository
            .Setup(repo => repo.GetPagedAsync(parameters))
            .ReturnsAsync(mockResult);

        // Act
        var response = await _sut.GetPagedAsync(parameters);

        // Assert
        response.IsSucceeded.Should().BeTrue();
        response.Status.Should().Be(200);
        response.Resources.Should().BeEquivalentTo(mockResult);
        _tokenRepository.Verify(repo => repo.GetPagedAsync(parameters), Times.Once);
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "Create")]
    public async Task CreateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateAsync(new CreateUserVerificationTokenDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "CreateList")]
    public async Task CreateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.CreateListAsync(new List<CreateUserVerificationTokenDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "GetAll")]
    public async Task GetAllAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetAllAsync();
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "GetById")]
    public async Task GetByIdAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.GetByIdAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "GetPagedSearchQuery")]
    public async Task GetPagedAsync_SearchQuery_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new SearchQuery());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "GetPagedAdvanced")]
    public async Task GetPagedAsync_Advanced_Throws_NotImplementedException()
    {
        var action = () => _sut.GetPagedAsync(new AdvancedSearchQuery<object>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDeleteAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteAsync(1);
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "SoftDeleteList")]
    public async Task SoftDeleteListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.SoftDeleteListAsync(new List<int>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "Update")]
    public async Task UpdateAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateAsync(new UpdateUserVerificationTokenDto());
        await action.Should().ThrowAsync<NotImplementedException>();
    }

    [Fact]
    [Trait("Service", "UserVerificationToken")]
    [Trait("Method", "UpdateList")]
    public async Task UpdateListAsync_Throws_NotImplementedException()
    {
        var action = () => _sut.UpdateListAsync(new List<UpdateUserVerificationTokenDto>());
        await action.Should().ThrowAsync<NotImplementedException>();
    }
}
