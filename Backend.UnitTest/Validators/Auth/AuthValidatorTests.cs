using System;
using Backend.Application.DTOs.Auths;
using Backend.Application.Validators.Auths;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Auth;

public class AuthValidatorTests
{
    // ════════════════════════════════════════════════════════════════════════
    // LoginRequestDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly LoginRequestDtoValidator _loginValidator = new();

    [Fact]
    public void Login_Valid_Passes()
        => _loginValidator.Validate(new LoginRequestDto { Username = "user", Password = "pass" }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null, "pass")]
    [InlineData("", "pass")]
    [InlineData("user", null)]
    [InlineData("user", "")]
    public void Login_MissingFields_Fails(string? username, string? password)
        => _loginValidator.Validate(new LoginRequestDto { Username = username!, Password = password! }).IsValid.Should().BeFalse();

    // ════════════════════════════════════════════════════════════════════════
    // LogoutRequestDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly LogoutRequestDtoValidator _logoutValidator = new();

    [Fact]
    public void Logout_Valid_Passes()
        => _logoutValidator.Validate(new LogoutRequestDto { RefreshToken = "token-abc" }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Logout_MissingToken_Fails(string? token)
        => _logoutValidator.Validate(new LogoutRequestDto { RefreshToken = token! }).IsValid.Should().BeFalse();

    // ════════════════════════════════════════════════════════════════════════
    // RefreshTokenRequestDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly RefreshTokenRequestDtoValidator _refreshValidator = new();

    [Fact]
    public void Refresh_Valid_Passes()
        => _refreshValidator.Validate(new RefreshTokenRequestDto { AccessToken = "access", RefreshToken = "refresh" }).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null, "refresh")]
    [InlineData("access", null)]
    [InlineData("", "refresh")]
    [InlineData("access", "")]
    public void Refresh_MissingFields_Fails(string? access, string? refresh)
        => _refreshValidator.Validate(new RefreshTokenRequestDto { AccessToken = access!, RefreshToken = refresh! }).IsValid.Should().BeFalse();
}
