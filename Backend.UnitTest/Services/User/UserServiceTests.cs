using System;
using System.Linq.Expressions;
using Backend.Application.Constants;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Common;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Moq;

namespace Backend.UnitTest.Services.User;

/// <summary>
/// Unit tests cho UserService — tập trung vào các validation và business rules.
/// </summary>
public class UserServiceTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserRoleRepository> _userRoleRepo = new();
    private readonly Mock<IMenuRepository> _menuRepo = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IStorageService> _storageService = new();
    private readonly Mock<IUserVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailTemplateService> _emailTemplate = new();
    private readonly Mock<IEmailService<GoogleMailRequest>> _emailService = new();
    private readonly Mock<IUserSessionRepository> _sessionRepo = new();

    private readonly global::Backend.Application.Implements.UserService _sut;

    public UserServiceTests()
    {
        _sut = new global::Backend.Application.Implements.UserService(
            _userRepo.Object,
            _userRoleRepo.Object,
            _menuRepo.Object,
            _permissionRepo.Object,
            MockHelper.LoggerFactory().Object,
            _storageService.Object,
            _tokenRepo.Object,
            MockHelper.HostSettings(),
            _emailTemplate.Object,
            _emailService.Object,
            MockHelper.HttpContextAccessor().Object,
            _sessionRepo.Object
        );
    }

    // ════════════════════════════════════════════════════════════════════════
    // CreateAsync — validation
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateEmail_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = TestDataBuilder.ValidCreateUserDto();
        _userRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(true); // email đã tồn tại

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.User.DuplicatedEmail);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicatePhone_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = TestDataBuilder.ValidCreateUserDto();

        // Lần 1: email OK, lần 2: phone duplicate
        _userRepo.SetupSequence(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false)  // email check → not duplicated
                 .ReturnsAsync(true);  // phone check → duplicated

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.User.DuplicatedPhoneNumber);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Create")]
    public async Task Create_DuplicateIdentityNumber_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = TestDataBuilder.ValidCreateUserDto();

        _userRepo.SetupSequence(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false)  // email OK
                 .ReturnsAsync(false)  // phone OK
                 .ReturnsAsync(true);  // identity → duplicate

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.User.DuplicatedIdentityNumber);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Create")]
    public async Task Create_RepositoryThrowsException_ReturnsInternalServerError()
    {
        // Arrange
        var dto = TestDataBuilder.ValidCreateUserDto();

        _userRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false);
        _userRepo.Setup(r => r.BeginTransactionAsync())
                 .Returns(Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(null!));
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.User>()))
                 .ThrowsAsync(new Exception("DB error"));

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(500);
    }

    // ════════════════════════════════════════════════════════════════════════
    // SoftDeleteAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync((Domain.Entities.User?)null);

        // Act
        var result = await _sut.SoftDeleteAsync(id: 999);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
    }

    // ════════════════════════════════════════════════════════════════════════
    // GetByIdAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "GetById")]
    public async Task GetById_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        _userRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), It.IsAny<bool>()))
                 .Returns(Enumerable.Empty<Domain.Entities.User>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.GetByIdAsync(id: 9999);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ChangePasswordAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "ChangePassword")]
    public async Task ChangePassword_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new ChangePasswordDto { OldPassword = "old", NewPassword = "new", ConfirmNewPassword = "new" };
        _userRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), It.IsAny<bool>()))
                 .Returns(Enumerable.Empty<Domain.Entities.User>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.ChangePasswordAsync(userId: 1, dto);

        // Assert
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "ChangePassword")]
    public async Task ChangePassword_WrongOldPassword_ReturnsBadRequest()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser(); // password hashed "Test@12345"
        var dto = new ChangePasswordDto
        {
            OldPassword = "WrongOldPassword",
            NewPassword = "NewPass@123",
            ConfirmNewPassword = "NewPass@123"
        };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id))
             .ReturnsAsync(user);

        // Act
        var result = await _sut.ChangePasswordAsync(userId: user.Id, dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "ChangePassword")]
    public async Task ChangePassword_NewPasswordMismatch_ReturnsBadRequest()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var dto = new ChangePasswordDto
        {
            OldPassword = "Test@12345",
            NewPassword = "NewPass@123",
            ConfirmNewPassword = "DifferentPass@456"
        };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id))
             .ReturnsAsync(user);

        // Act
        var result = await _sut.ChangePasswordAsync(userId: user.Id, dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
    }
}

