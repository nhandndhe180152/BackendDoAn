using System;
using Backend.Application.DTOs.Suppliers;
using Backend.Application.Validators.Suppliers;
using FluentAssertions;
using Xunit;

namespace Backend.UnitTest.Validators.Suppliers;

/// <summary>
/// Unit tests cho validator của Supplier (Create + Update).
/// </summary>
public class SupplierValidatorTests
{
    private readonly CreateSupplierDtoValidator _createValidator = new();
    private readonly UpdateSupplierDtoValidator _updateValidator = new();

    private static CreateSupplierDto ValidCreate() => new()
    {
        Name = "Công ty TNHH ABC",
        Code = "SUP001",
        ContactPerson = "Nguyễn Văn A",
        Phone = "0901234567",
        Email = "abc@supplier.com",
        Address = "123 Lê Lợi",
        TaxCode = "0312345678",
        IsActive = true
    };

    private static UpdateSupplierDto ValidUpdate() => new()
    {
        Id = 1,
        Name = "Công ty TNHH ABC",
        Code = "SUP001",
        Email = "abc@supplier.com",
        IsActive = true
    };

    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_Valid_Passes()
        => _createValidator.Validate(ValidCreate()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_NameRequired_Fails(string? name)
    {
        var dto = ValidCreate(); dto.Name = name!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_NameTooLong_Fails()
    {
        var dto = ValidCreate(); dto.Name = new string('A', 256);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_CodeRequired_Fails(string? code)
    {
        var dto = ValidCreate(); dto.Code = code!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_CodeTooLong_Fails()
    {
        var dto = ValidCreate(); dto.Code = new string('A', 51);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_InvalidEmail_Fails()
    {
        var dto = ValidCreate(); dto.Email = "not-an-email";
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Create_EmailOptional_Passes(string? email)
    {
        var dto = ValidCreate(); dto.Email = email;
        _createValidator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Create_ContactPersonTooLong_Fails()
    {
        var dto = ValidCreate(); dto.ContactPerson = new string('A', 256);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_PhoneTooLong_Fails()
    {
        var dto = ValidCreate(); dto.Phone = new string('9', 51);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_AddressTooLong_Fails()
    {
        var dto = ValidCreate(); dto.Address = new string('A', 501);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_TaxCodeTooLong_Fails()
    {
        var dto = ValidCreate(); dto.TaxCode = new string('1', 51);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Valid_Passes()
        => _updateValidator.Validate(ValidUpdate()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Update_InvalidId_Fails(int id)
    {
        var dto = ValidUpdate(); dto.Id = id;
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Update_NameRequired_Fails(string? name)
    {
        var dto = ValidUpdate(); dto.Name = name!;
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Update_CodeRequired_Fails(string? code)
    {
        var dto = ValidUpdate(); dto.Code = code!;
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Update_InvalidEmail_Fails()
    {
        var dto = ValidUpdate(); dto.Email = "bad@";
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }
}
