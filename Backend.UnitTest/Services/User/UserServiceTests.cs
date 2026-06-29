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

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Create")]
    public async Task Create_ValidUser_CreatesUserRolesAndCommits()
    {
        // Arrange
        var dto = TestDataBuilder.ValidCreateUserDto();
        Domain.Entities.User? createdUser = null;
        IEnumerable<Domain.Entities.UserRole>? createdRoles = null;

        _userRepo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Domain.Entities.User, bool>>>()))
                 .ReturnsAsync(false);
        _userRepo.Setup(r => r.BeginTransactionAsync())
                 .Returns(Task.FromResult<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(null!));
        _userRepo.Setup(r => r.CreateAsync(It.IsAny<Domain.Entities.User>()))
                 .Callback<Domain.Entities.User>(user => createdUser = user)
                 .Returns(Task.CompletedTask);
        _userRoleRepo.Setup(r => r.CreateListAsync(It.IsAny<IEnumerable<Domain.Entities.UserRole>>()))
                     .Callback<IEnumerable<Domain.Entities.UserRole>>(roles => createdRoles = roles.ToList())
                     .Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);
        _userRepo.Setup(r => r.EndTransactionAsync()).Returns(Task.CompletedTask);

        // Act
        var result = await _sut.CreateAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        result.Status.Should().Be(201);
        createdUser.Should().NotBeNull();
        createdUser!.Email.Should().Be(dto.Email);
        createdUser.FirstName.Should().Be(dto.FirstName);
        createdRoles.Should().NotBeNull();
        createdRoles!.Select(x => x.RoleId).Should().BeEquivalentTo(dto.Roles);
        _userRepo.Verify(r => r.CreateAsync(It.IsAny<Domain.Entities.User>()), Times.Once);
        _userRoleRepo.Verify(r => r.CreateListAsync(It.IsAny<IEnumerable<Domain.Entities.UserRole>>()), Times.Once);
        _userRepo.Verify(r => r.EndTransactionAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "GetById")]
    public async Task GetById_ExistingUser_ReturnsUserDetailWithRoles()
    {
        // Arrange
        var user = CreateUser(id: 5, username: "staff01", firstName: "Nguyen", lastName: "An");
        SetupUsers([user]);

        // Act
        var result = await _sut.GetByIdAsync(5);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        var data = result.Resources.Should().BeOfType<UserDetailDto>().Subject;
        data.Id.Should().Be(5);
        data.Username.Should().Be("staff01");
        data.UserStatus.Name.Should().Be("Active");
        data.Roles.Should().ContainSingle(x => x.Name == "Staff");
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "GetProfile")]
    public async Task GetProfile_ExistingUser_ReturnsProfile()
    {
        // Arrange
        var user = CreateUser(id: 7, username: "profile01", firstName: "Tran", lastName: "Binh");
        SetupUsers([user]);

        // Act
        var result = await _sut.GetProfileAsync(7);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        var data = result.Resources.Should().BeOfType<UserProfileDto>().Subject;
        data.Id.Should().Be(7);
        data.Username.Should().Be("profile01");
        data.UserRoles.Should().ContainSingle(x => x.Name == "Staff");
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "GetProfile")]
    public async Task GetProfile_MissingUser_ReturnsNotFound()
    {
        // Arrange
        SetupUsers([]);

        // Act
        var result = await _sut.GetProfileAsync(404);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        result.Code.Should().Be(ApiCodeConstants.Auth.UserNotFound);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Search")]
    public async Task GetPaged_SearchQuery_FiltersByKeyword()
    {
        // Arrange
        var users = new List<Domain.Entities.User>
        {
            CreateUser(id: 1, username: "staff01", firstName: "Nguyen", lastName: "An", email: "an@example.com"),
            CreateUser(id: 2, username: "stock02", firstName: "Tran", lastName: "Binh", email: "binh@example.com"),
            CreateUser(id: 3, username: "demo03", firstName: "Le", lastName: "Chi", email: "chi@example.com")
        };
        SetupUsers(users);

        var query = new SearchQuery
        {
            PageIndex = 1,
            PageSize = 10,
            Keyword = "stock"
        };

        // Act
        var result = await _sut.GetPagedAsync(query);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        var data = result.Resources.Should().BeOfType<PagingData<UserListDto>>().Subject;
        data.Total.Should().Be(3);
        data.TotalFiltered.Should().Be(1);
        data.DataSource.Should().ContainSingle(x => x.Username == "stock02");
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_WhenRepositoryDeletes_ReturnsSuccess()
    {
        // Arrange
        _userRepo.Setup(r => r.SoftDeleteAsync(3)).ReturnsAsync(true);
        _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.SoftDeleteAsync(3);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        result.Resources.Should().Be(true);
        _userRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "SoftDelete")]
    public async Task SoftDelete_WhenRepositoryCannotDelete_ReturnsBadRequest()
    {
        // Arrange
        _userRepo.Setup(r => r.SoftDeleteAsync(404)).ReturnsAsync(false);

        // Act
        var result = await _sut.SoftDeleteAsync(404);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(400);
        _userRepo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "ChangePassword")]
    public async Task ChangePassword_ValidPassword_UpdatesPasswordAndSaves()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var oldPasswordHash = user.PasswordHash;
        var dto = new ChangePasswordDto
        {
            OldPassword = "Test@12345",
            NewPassword = "NewPass@123",
            ConfirmNewPassword = "NewPass@123"
        };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.ChangePasswordAsync(user.Id, dto);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        user.PasswordHash.Should().NotBe(oldPasswordHash);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _userRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "UpdateProfile")]
    public async Task UpdateProfile_ExistingUser_UpdatesBasicProfile()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        var dto = new UpdateUserProfileDto
        {
            FirstName = "New",
            LastName = "Name",
            PhoneNumber = "0988000111",
            Gender = 1,
            IdentityNumber = "123456789012",
            AddresDetail = "Ha Noi",
            AvatarId = 10
        };

        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
        _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.UpdateProfileAsync(user.Id, dto);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        user.FirstName.Should().Be("New");
        user.LastName.Should().Be("Name");
        user.PhoneNumber.Should().Be("0988000111");
        user.AvatarId.Should().Be(10);
        user.UpdatedBy.Should().Be(user.Id);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _userRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "UpdateProfile")]
    public async Task UpdateProfile_MissingUser_ReturnsNotFound()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((Domain.Entities.User?)null);

        var dto = new UpdateUserProfileDto
        {
            FirstName = "New",
            LastName = "Name"
        };

        // Act
        var result = await _sut.UpdateProfileAsync(404, dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        result.Code.Should().Be(ApiCodeConstants.Auth.UserNotFound);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Deactivate")]
    public async Task Deactivate_ExistingUser_DeactivatesUserAndSessions()
    {
        // Arrange
        var user = TestDataBuilder.DefaultUser();
        _userRepo.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepo.Setup(r => r.UpdateAsync(user)).Returns(Task.CompletedTask);
        _sessionRepo.Setup(r => r.SoftDeleteAsync(It.IsAny<Expression<Func<Domain.Entities.UserSession, bool>>>()))
                    .ReturnsAsync(true);
        _userRepo.Setup(r => r.SaveChangesAsync()).ReturnsAsync(1);

        // Act
        var result = await _sut.Deactivate(user.Id);

        // Assert
        result.IsSucceeded.Should().BeTrue();
        user.UserStatusId.Should().Be((int)Domain.Enums.Enums.UserStatus.Deactivated);
        _userRepo.Verify(r => r.UpdateAsync(user), Times.Once);
        _sessionRepo.Verify(r => r.SoftDeleteAsync(It.IsAny<Expression<Func<Domain.Entities.UserSession, bool>>>()), Times.Once);
        _userRepo.Verify(r => r.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    [Trait("Service", "User")]
    [Trait("Method", "Deactivate")]
    public async Task Deactivate_MissingUser_ReturnsNotFound()
    {
        // Arrange
        _userRepo.Setup(r => r.GetByIdAsync(404)).ReturnsAsync((Domain.Entities.User?)null);

        // Act
        var result = await _sut.Deactivate(404);

        // Assert
        result.IsSucceeded.Should().BeFalse();
        result.Status.Should().Be(404);
        _sessionRepo.Verify(r => r.SoftDeleteAsync(It.IsAny<Expression<Func<Domain.Entities.UserSession, bool>>>()), Times.Never);
    }

    private void SetupUsers(List<Domain.Entities.User> users)
    {
        _userRepo.Setup(r => r.FindByCondition(
                It.IsAny<Expression<Func<Domain.Entities.User, bool>>>(),
                It.IsAny<bool>()))
            .Returns((Expression<Func<Domain.Entities.User, bool>> predicate, bool _) =>
                users.AsQueryable().Where(predicate).BuildMock());
    }

    private static Domain.Entities.User CreateUser(
        int id,
        string username,
        string firstName,
        string lastName,
        string? email = null)
    {
        var role = new Domain.Entities.Role
        {
            Id = 2,
            Name = "Staff",
            IsDeleted = false
        };

        var user = TestDataBuilder.DefaultUser(u =>
        {
            u.Id = id;
            u.Username = username;
            u.FirstName = firstName;
            u.LastName = lastName;
            u.Email = email ?? $"{username}@example.com";
            u.UserStatusId = (int)Domain.Enums.Enums.UserStatus.Actived;
            u.UserStatus = new Domain.Entities.UserStatus
            {
                Id = (int)Domain.Enums.Enums.UserStatus.Actived,
                Name = "Active",
                Color = "green",
                IsDeleted = false
            };
            u.UserRoles = new List<Domain.Entities.UserRole>
            {
                new()
                {
                    Id = id * 10,
                    UserId = id,
                    RoleId = role.Id,
                    Role = role,
                    IsDeleted = false
                }
            };
        });

        return user;
    }
}

