using Backend.Application.Constants;
using Backend.Application.DTOs.Auths;
using Backend.Application.DTOs.Emails;
using Backend.Application.Implements;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.UnitTest.Common;
using Backend.UnitTest.Fixtures;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using System.Linq.Expressions;

namespace Backend.UnitTest.Services.Auth;

/// <summary>
/// Unit tests cho AuthService.
/// Mock toàn bộ repository và external services — không cần DB thật.
/// </summary>
public class AuthServiceTests
{
    // ── Mocks ────────────────────────────────────────────────────────────────
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<IUserSessionRepository> _sessionRepo = new();
    private readonly Mock<IUserRoleRepository> _userRoleRepo = new();
    private readonly Mock<ITokenProviderService> _tokenProvider = new();
    private readonly Mock<IStorageService> _storageService = new();
    private readonly Mock<IPermissionRepository> _permissionRepo = new();
    private readonly Mock<IUserVerificationTokenRepository> _tokenRepo = new();
    private readonly Mock<IEmailService<GoogleMailRequest>> _emailService = new();
    private readonly Mock<IEmailTemplateService> _emailTemplate = new();
    private readonly Mock<IMenuRepository> _menuRepo = new();
    private readonly Mock<IRoleRepository> _roleRepo = new();
    private readonly Mock<IFileUploadRepository> _fileUploadRepo = new();

    private readonly AuthService _sut; // System Under Test

    public AuthServiceTests()
    {
        _sut = new AuthService(
            _userRepo.Object,
            _sessionRepo.Object,
            _tokenProvider.Object,
            _userRoleRepo.Object,
            _storageService.Object,
            _permissionRepo.Object,
            _emailService.Object,
            _tokenRepo.Object,
            MockHelper.HttpContextAccessor().Object,
            MockHelper.HostSettings(),
            _emailTemplate.Object,
            _menuRepo.Object,
            _roleRepo.Object,
            MockHelper.LoggerFactory().Object,
            _fileUploadRepo.Object
        );
    }

    // ════════════════════════════════════════════════════════════════════════
    // ForgotPasswordAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ForgotPassword")]
    public async Task ForgotPassword_EmailNotFound_ReturnsBadRequest()
    {
        // Arrange
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync((Domain.Entities.User?)null);

        // Act
        var result = await _sut.ForgotPasswordAsync("notfound@example.com");

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        result.Code.Should().Be(ApiCodeConstants.Auth.EmailNotFound);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ForgotPassword")]
    public async Task ForgotPassword_UserNotActivated_ReturnsBadRequest()
    {
        // Arrange
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync(TestDataBuilder.NotActivatedUser());

        // Act
        var result = await _sut.ForgotPasswordAsync("test@example.com");

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        result.Code.Should().Be(ApiCodeConstants.Auth.UserNotActivated);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ForgotPassword")]
    public async Task ForgotPassword_UserDeactivated_ReturnsBadRequest()
    {
        // Arrange
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync(TestDataBuilder.DeactivatedUser());

        // Act
        var result = await _sut.ForgotPasswordAsync("test@example.com");

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.UserDeactivated);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ForgotPassword")]
    public async Task ForgotPassword_UserLocked_ReturnsBadRequest()
    {
        // Arrange
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync(TestDataBuilder.LockedUser());

        // Act
        var result = await _sut.ForgotPasswordAsync("test@example.com");

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.UserLocked);
    }

    // ════════════════════════════════════════════════════════════════════════
    // ResetPasswordAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ResetPassword")]
    public async Task ResetPassword_PasswordMismatch_ReturnsBadRequest()
    {
        // Arrange
        var dto = new ResetPasswordDto
        {
            Email = "test@example.com",
            Code = "abc",
            NewPassword = "Pass@123",
            ConfirmPassword = "Different@456",
            Purpose = "FORGOT_PASSWORD"
        };

        // Act
        var result = await _sut.ResetPasswordAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        result.Code.Should().Be(ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ResetPassword")]
    public async Task ResetPassword_EmailNotFound_ReturnsBadRequest()
    {
        // Arrange
        var dto = TestDataBuilder.ValidResetPasswordDto();
        _userRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), It.IsAny<bool>()))
                 .Returns(Enumerable.Empty<Domain.Entities.User>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.ResetPasswordAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.EmailNotFound);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "ResetPassword")]
    public async Task ResetPassword_TokenExpiredOrNotFound_ReturnsBadRequest()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var dto = TestDataBuilder.ValidResetPasswordDto("invalid-code");

        _userRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), It.IsAny<bool>()))
                 .Returns(new[] { user }.AsQueryable().BuildMock());

        _tokenRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<UserVerificationToken, bool>>>(), It.IsAny<bool>()))
                  .Returns(Enumerable.Empty<UserVerificationToken>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.ResetPasswordAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.VerificationCodeHasExpired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // VerifyCodeAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "VerifyCode")]
    public async Task VerifyCode_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new VerifyCodeDto { Email = "ghost@example.com", Code = "xyz", Purpose = "FORGOT_PASSWORD" };
        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync((Domain.Entities.User?)null);

        // Act
        var result = await _sut.VerifyCodeAsync(dto);

        // Assert
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "VerifyCode")]
    public async Task VerifyCode_TokenAlreadyUsed_ReturnsBadRequest()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var token = TestDataBuilder.UsedToken(user.Id);
        var dto = new VerifyCodeDto { Email = user.Email, Code = token.Code, Purpose = token.Purpose };

        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync(user);
        _tokenRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserVerificationToken, bool>>>(), false))
                  .ReturnsAsync(token);

        // Act
        var result = await _sut.VerifyCodeAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.VerificationCodeUsed);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "VerifyCode")]
    public async Task VerifyCode_TokenExpired_ReturnsBadRequest()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var token = TestDataBuilder.ExpiredToken(user.Id);
        var dto = new VerifyCodeDto { Email = user.Email, Code = token.Code, Purpose = token.Purpose };

        _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(), false))
                 .ReturnsAsync(user);
        _tokenRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<Expression<Func<UserVerificationToken, bool>>>(), false))
                  .ReturnsAsync(token);

        // Act
        var result = await _sut.VerifyCodeAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Code.Should().Be(ApiCodeConstants.Auth.VerificationCodeHasExpired);
    }

    // ════════════════════════════════════════════════════════════════════════
    // LogoutAsync
    // ════════════════════════════════════════════════════════════════════════

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "Logout")]
    public async Task Logout_SessionNotFound_ReturnsNotFound()
    {
        // Arrange
        var dto = new LogoutRequestDto { RefreshToken = "unknown-token" };
        _sessionRepo.Setup(r => r.FindByCondition(It.IsAny<Expression<Func<UserSession, bool>>>(), It.IsAny<bool>()))
                    .Returns(Enumerable.Empty<UserSession>().AsQueryable().BuildMock());

        // Act
        var result = await _sut.LogoutAsync(dto, userId: 1);

        // Assert
        result.Status.Should().Be(404);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "Logout")]
    public async Task LogoutAllDevices_NoActiveSessions_ReturnsSuccess()
    {
        // Arrange
        _sessionRepo.Setup(r => r.FindByConditionAsync(It.IsAny<Expression<Func<UserSession, bool>>>(), It.IsAny<bool>()))
                    .ReturnsAsync(new List<UserSession>());

        // Act
        var result = await _sut.LogoutAllDeviceAsync(userId: 1);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        _sessionRepo.Verify(r => r.UpdateListAsync(It.IsAny<List<UserSession>>()), Times.Never);
    }

    // ════════════════════════════════════════════════════════════════════════
    // RegisterAsync — validation cases
    // ════════════════════════════════════════════════════════════════════════

    [Theory]
    [Trait("Service", "Auth")]
    [Trait("Method", "Register")]
    [InlineData("ab")]            // quá ngắn
    [InlineData("có dấu")]        // có ký tự đặc biệt
    [InlineData("has space")]     // có khoảng trắng
    [InlineData("")]              // rỗng
    public async Task Register_InvalidUsername_ReturnsUnprocessableEntity(string username)
    {
        // Arrange
        var dto = TestDataBuilder.ValidSignUpDto();
        dto.UserName = username;

        // Act
        var result = await _sut.RegisterAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
    }

    [Fact]
    [Trait("Service", "Auth")]
    [Trait("Method", "Register")]
    public async Task Register_DuplicateUsername_ReturnsUnprocessableEntity()
    {
        // Arrange
        var dto = TestDataBuilder.ValidSignUpDto();
        _userRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(true); // username đã tồn tại

        // Act
        var result = await _sut.RegisterAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
        result.Code.Should().Be(ApiCodeConstants.Common.DuplicatedData);
    }

    [Theory]
    [Trait("Service", "Auth")]
    [Trait("Method", "Register")]
    [InlineData("notanemail")]
    [InlineData("missing@")]
    [InlineData("@nodomain.com")]
    [InlineData("")]
    public async Task Register_InvalidEmailFormat_ReturnsUnprocessableEntity(string email)
    {
        // Arrange — username valid, email invalid
        var dto = TestDataBuilder.ValidSignUpDto();
        dto.Email = email;
        _userRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false);

        // Act
        var result = await _sut.RegisterAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
    }

    [Theory]
    [Trait("Service", "Auth")]
    [Trait("Method", "Register")]
    [InlineData("0112345678")] // đầu số sai
    [InlineData("09123")]      // thiếu số
    [InlineData("abcdefghij")] // không phải số
    [InlineData("")]           // rỗng
    public async Task Register_InvalidPhoneNumber_ReturnsUnprocessableEntity(string phone)
    {
        // Arrange — username & email valid, phone invalid
        var dto = TestDataBuilder.ValidSignUpDto();
        dto.PhoneNumber = phone;

        // email check pass
        _userRepo.SetupSequence(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false)  // username not duplicated
                 .ReturnsAsync(false); // email not duplicated

        // Act
        var result = await _sut.RegisterAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(422);
    }
}
