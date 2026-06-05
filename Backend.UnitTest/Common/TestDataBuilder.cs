using System;
using Backend.Application.DTOs.Auths;
using Backend.Application.DTOs.Users;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Share.Helpers;

namespace Backend.UnitTest.Common;

/// <summary>
/// Builder cung cấp dữ liệu test mặc định, có thể override theo từng test case.
/// Dùng pattern: TestDataBuilder.DefaultUser().With(x => x.Email = "custom@test.com")
/// </summary>
public static class TestDataBuilder
{
    // ── User Entity ──────────────────────────────────────────────────────────
    public static User DefaultUser(Action<User>? configure = null)
    {
        var user = new User
        {
            Id = 1,
            Username = "testuser01",
            Email = "test@example.com",
            PhoneNumber = "0912345678",
            FirstName = "Test",
            LastName = "User",
            PasswordHash = PasswordHelper.HashPassword("Test@12345"),
            UserStatusId = (int)Enums.UserStatus.Actived,
            LockEnabled = false,
            AccessFailedCount = 0,
            IsDeleted = false,
            CreatedDate = DateTime.Now.AddDays(-10),
        };
        configure?.Invoke(user);
        return user;
    }

    public static User LockedUser() => DefaultUser(u =>
    {
        u.UserStatusId = (int)Enums.UserStatus.Locked;
        u.LockEnabled = true;
        u.LockEndDate = DateTime.Now.AddHours(2);
        u.AccessFailedCount = 5;
    });

    public static User NotActivatedUser() => DefaultUser(u =>
    {
        u.UserStatusId = (int)Enums.UserStatus.NotActivated;
    });

    public static User DeactivatedUser() => DefaultUser(u =>
    {
        u.UserStatusId = (int)Enums.UserStatus.Deactivated;
    });

    // ── Auth DTOs ────────────────────────────────────────────────────────────
    public static LoginRequestDto ValidLoginRequest() => new()
    {
        Username = "testuser01",
        Password = "Test@12345"
    };

    public static LoginRequestDto WrongPasswordLoginRequest() => new()
    {
        Username = "testuser01",
        Password = "WrongPassword!"
    };

    public static UserSignUpDto ValidSignUpDto() => new()
    {
        UserName = "newuser01",
        Email = "newuser@example.com",
        PhoneNumber = "0987654321",
        IdentityNumber = "123456789012",
        Password = "Test@12345",
    };

    public static ResetPasswordDto ValidResetPasswordDto(string code = "valid-code-abc") => new()
    {
        Email = "test@example.com",
        Code = code,
        NewPassword = "NewPass@123",
        ConfirmPassword = "NewPass@123",
        Purpose = "FORGOT_PASSWORD"
    };

    // ── User Create/Update DTOs ──────────────────────────────────────────────
    public static CreateUserDto ValidCreateUserDto() => new()
    {
        Email = "admin@example.com",
        PhoneNumber = "0901234567",
        IdentityNumber = "098765432101",
        FirstName = "Admin",
        LastName = "New",
        PasswordHash = "Test@12345",
        Roles = new List<int> { 1001 },
        CreatedBy = 1
    };

    // ── UserVerificationToken ────────────────────────────────────────────────
    public static UserVerificationToken ValidToken(int userId = 1, string code = "valid-code-abc") => new()
    {
        Id = 1,
        UserId = userId,
        Code = code,
        Purpose = "FORGOT_PASSWORD",
        ExpirationDate = DateTime.Now.AddHours(2),
        IsUsed = false,
        IsDeleted = false,
        CreatedDate = DateTime.Now.AddMinutes(-5)
    };

    public static UserVerificationToken ExpiredToken(int userId = 1)
    {
        var token = ValidToken(userId);
        token.ExpirationDate = DateTime.Now.AddHours(-1);
        return token;
    }

    public static UserVerificationToken UsedToken(int userId = 1)
    {
        var token = ValidToken(userId);
        token.IsUsed = true;
        return token;
    }
}
