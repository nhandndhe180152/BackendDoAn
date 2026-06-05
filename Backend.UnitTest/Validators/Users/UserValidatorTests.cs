using System;
using Backend.Application.DTOs.Users;
using Backend.Application.Validators.Users;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Users;

public class UserValidatorTests
{
    // ════════════════════════════════════════════════════════════════════════
    // CreateUserDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly CreateUserDtoValidator _createValidator = new();

    private static CreateUserDto ValidCreateUser() => new()
    {
        PasswordHash = "Abcd@1234",
        FirstName = "Nguyen",
        LastName = "An",
        Email = "user@example.com",
        PhoneNumber = null,
        Gender = null,
        IdentityNumber = null,
        Roles = new List<int> { 1 }
    };

    [Fact]
    public void CreateUser_Valid_Passes()
        => _createValidator.Validate(ValidCreateUser()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")] // < 8 chars
    public void CreateUser_InvalidPassword_Fails(string? pw)
    {
        var dto = ValidCreateUser(); dto.PasswordHash = pw!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateUser_PasswordTooLong_Fails()
    {
        var dto = ValidCreateUser(); dto.PasswordHash = new string('A', 65);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateUser_MissingFirstName_Fails(string? val)
    {
        var dto = ValidCreateUser(); dto.FirstName = val!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateUser_FirstNameTooLong_Fails()
    {
        var dto = ValidCreateUser(); dto.FirstName = new string('A', 256);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateUser_MissingLastName_Fails(string? val)
    {
        var dto = ValidCreateUser(); dto.LastName = val!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void CreateUser_MissingEmail_Fails(string? val)
    {
        var dto = ValidCreateUser(); dto.Email = val!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void CreateUser_EmailTooLong_Fails()
    {
        var dto = ValidCreateUser(); dto.Email = new string('a', 496) + "@e.com";
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("0912345678")] // valid
    [InlineData(null)]         // optional — valid
    [InlineData("")]           // optional — valid
    public void CreateUser_PhoneOptional_ValidCases_Pass(string? phone)
    {
        var dto = ValidCreateUser(); dto.PhoneNumber = phone;
        _createValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("0112345678")]  // sai đầu số
    [InlineData("09123")]       // quá ngắn
    public void CreateUser_InvalidPhone_Fails(string phone)
    {
        var dto = ValidCreateUser(); dto.PhoneNumber = phone;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(0)]  // Female
    [InlineData(1)]  // Male
    public void CreateUser_ValidGender_Passes(int? gender)
    {
        var dto = ValidCreateUser(); dto.Gender = gender;
        _createValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateUser_InvalidGender_Fails()
    {
        var dto = ValidCreateUser(); dto.Gender = 99;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("123456789012")]  // valid
    [InlineData(null)]            // optional
    [InlineData("")]              // optional
    public void CreateUser_IdentityNumber_OptionalValidCases_Pass(string? idNum)
    {
        var dto = ValidCreateUser(); dto.IdentityNumber = idNum;
        _createValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("12345")]         // too short
    [InlineData("1234567890123")] // too long
    public void CreateUser_InvalidIdentityNumber_Fails(string idNum)
    {
        var dto = ValidCreateUser(); dto.IdentityNumber = idNum;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    // ════════════════════════════════════════════════════════════════════════
    // AdminRegisterDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly AdminRegisterDtoValidator _adminRegisterValidator = new();

    private static AdminRegisterDto ValidAdminRegister() => new()
    {
        Username = "admin01",
        Password = "Abcd@1234",
        FirstName = "Admin",
        LastName = "User",
        Email = "admin@example.com"
    };

    [Fact]
    public void AdminRegister_Valid_Passes()
        => _adminRegisterValidator.Validate(ValidAdminRegister()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ab")]          // < 6 chars
    public void AdminRegister_InvalidUsername_Fails(string? username)
    {
        var dto = ValidAdminRegister(); dto.Username = username!;
        _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AdminRegister_UsernameTooLong_Fails()
    {
        var dto = ValidAdminRegister(); dto.Username = new string('a', 31);
        _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("short")] // < 8
    public void AdminRegister_InvalidPassword_Fails(string? pw)
    {
        var dto = ValidAdminRegister(); dto.Password = pw!;
        _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AdminRegister_MissingFirstName_Fails(string? val)
    { var dto = ValidAdminRegister(); dto.FirstName = val!; _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AdminRegister_MissingLastName_Fails(string? val)
    { var dto = ValidAdminRegister(); dto.LastName = val!; _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AdminRegister_MissingEmail_Fails(string? val)
    { var dto = ValidAdminRegister(); dto.Email = val!; _adminRegisterValidator.Validate(dto).IsValid.Should().BeFalse(); }

    // ════════════════════════════════════════════════════════════════════════
    // UpdateProfileDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly UpdateProfileDtoValidator _profileValidator = new();

    private static UpdateUserProfileDto ValidProfile() => new()
    {
        FirstName = "Nguyen",
        LastName = "An",
        PhoneNumber = null,
        Gender = null,
        IdentityNumber = null
    };

    [Fact]
    public void UpdateProfile_Valid_Passes()
        => _profileValidator.Validate(ValidProfile()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateProfile_MissingFirstName_Fails(string? val)
    { var dto = ValidProfile(); dto.FirstName = val!; _profileValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void UpdateProfile_MissingLastName_Fails(string? val)
    { var dto = ValidProfile(); dto.LastName = val!; _profileValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void UpdateProfile_InvalidPhone_Fails()
    { var dto = ValidProfile(); dto.PhoneNumber = "0112345678"; _profileValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void UpdateProfile_InvalidGender_Fails()
    { var dto = ValidProfile(); dto.Gender = 99; _profileValidator.Validate(dto).IsValid.Should().BeFalse(); }

    [Fact]
    public void UpdateProfile_InvalidIdentityNumber_Fails()
    { var dto = ValidProfile(); dto.IdentityNumber = "123"; _profileValidator.Validate(dto).IsValid.Should().BeFalse(); }

    // ════════════════════════════════════════════════════════════════════════
    // ChangePasswordDtoValidator
    // ════════════════════════════════════════════════════════════════════════
    private readonly ChangePasswordDtoValidator _changePwValidator = new();

    [Fact]
    public void ChangePassword_ValidLongPassword_Passes()
        => _changePwValidator.Validate(new ChangePasswordDto { OldPassword = "old", NewPassword = "NewPass@123456", ConfirmNewPassword = "NewPass@123456" }).IsValid.Should().BeTrue();

    [Fact]
    public void ChangePassword_NewPasswordTooShort_Fails()
        => _changePwValidator.Validate(new ChangePasswordDto { OldPassword = "old", NewPassword = "Short1", ConfirmNewPassword = "Short1" }).IsValid.Should().BeFalse();
}
