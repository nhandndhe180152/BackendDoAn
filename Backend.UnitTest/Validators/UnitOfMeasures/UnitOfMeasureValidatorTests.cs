using System;
using Backend.Application.DTOs.UnitOfMeasures;
using Backend.Application.Validators.UnitOfMeasures;
using FluentAssertions;
using Xunit;

namespace Backend.UnitTest.Validators.UnitOfMeasures;

/// <summary>
/// Unit tests cho validator của UnitOfMeasure (Create + Update).
/// </summary>
public class UnitOfMeasureValidatorTests
{
    private readonly CreateUnitOfMeasureDtoValidator _createValidator = new();
    private readonly UpdateUnitOfMeasureDtoValidator _updateValidator = new();

    private static CreateUnitOfMeasureDto ValidCreate() => new()
    {
        Name = "Kilôgram",
        Symbol = "kg"
    };

    private static UpdateUnitOfMeasureDto ValidUpdate() => new()
    {
        Id = 1,
        Name = "Kilôgram",
        Symbol = "kg"
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
    public void Create_SymbolRequired_Fails(string? symbol)
    {
        var dto = ValidCreate(); dto.Symbol = symbol!;
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Create_SymbolTooLong_Fails()
    {
        var dto = ValidCreate(); dto.Symbol = new string('x', 51);
        _createValidator.Validate(dto).IsValid.Should().BeFalse();
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_Valid_Passes()
        => _updateValidator.Validate(ValidUpdate()).IsValid.Should().BeTrue();

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
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
    public void Update_SymbolRequired_Fails(string? symbol)
    {
        var dto = ValidUpdate(); dto.Symbol = symbol!;
        _updateValidator.Validate(dto).IsValid.Should().BeFalse();
    }
}
