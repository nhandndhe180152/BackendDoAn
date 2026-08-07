using Backend.Application.DTOs.StockTakes;
using Backend.Application.Validators.StockTakes;
using FluentAssertions;

namespace Backend.UnitTest.Validators.StockTake;

public class CreateStockTakeDtoValidatorTests
{
    private readonly CreateStockTakeDtoValidator _validator = new();
    private static CreateStockTakeDto Valid() => new() { WarehouseId = 1, Note = "OK", StockTakeItems = [new() { SystemQuantity = 0 }] };

    [Fact]
    public void Validate_ValidDto_Passes()
    {
        var result = _validator.Validate(Valid());
        result.IsValid.Should().BeTrue(); result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Validate_WarehouseIdIsNotPositive_Fails(int value)
    {
        var dto = Valid(); dto.WarehouseId = value;
        var result = _validator.Validate(dto);
        result.IsValid.Should().BeFalse(); result.Errors.Should().Contain(x => x.PropertyName == nameof(dto.WarehouseId));
    }

    [Fact]
    public void Validate_NoteAtMaximumLength_Passes()
    {
        var dto = Valid(); dto.Note = new string('a', 500);
        _validator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NoteExceedsMaximumLength_Fails()
    {
        var dto = Valid(); dto.Note = new string('a', 501);
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == nameof(dto.Note));
    }

    [Fact]
    public void Validate_OneNestedItemIsInvalid_FailsForNestedProperty()
    {
        var dto = Valid(); dto.StockTakeItems[0].SystemQuantity = -1;
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == "StockTakeItems[0].SystemQuantity");
    }
}

public class UpdateStockTakeDtoValidatorTests
{
    private readonly UpdateStockTakeDtoValidator _validator = new();
    private static UpdateStockTakeDto Valid() => new() { Id = 1, StockTakeStatusId = 1, Note = "OK", StockTakeItems = [new() { SystemQuantity = 0 }] };

    [Fact]
    public void Validate_ValidDto_Passes()
    {
        var result = _validator.Validate(Valid()); result.IsValid.Should().BeTrue(); result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("Id", 0)]
    [InlineData("Id", -1)]
    [InlineData("StockTakeStatusId", 0)]
    [InlineData("StockTakeStatusId", -1)]
    public void Validate_PositiveIdRuleIsViolated_Fails(string property, int value)
    {
        var dto = Valid(); if (property == nameof(dto.Id)) dto.Id = value; else dto.StockTakeStatusId = value;
        var result = _validator.Validate(dto);
        result.Errors.Should().Contain(x => x.PropertyName == property);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validate_NoteLength_ReturnsExpectedResult(int length, bool expected)
    {
        var dto = Valid(); dto.Note = new string('a', length);
        var result = _validator.Validate(dto); result.IsValid.Should().Be(expected);
        if (!expected) result.Errors.Should().Contain(x => x.PropertyName == nameof(dto.Note));
    }

    [Fact]
    public void Validate_OneNestedItemIsInvalid_FailsForNestedProperty()
    {
        var dto = Valid(); dto.StockTakeItems[0].ActualQuantity = -1;
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == "StockTakeItems[0].ActualQuantity");
    }
}

public class CreateStockTakeItemDtoValidatorTests
{
    private readonly CreateStockTakeItemDtoValidator _validator = new();
    private static CreateStockTakeItemDto Valid() => new() { SystemQuantity = 0, ActualQuantity = null, Note = "OK" };

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1")]
    public void Validate_ValidDto_Passes(string? actual)
    {
        var dto = Valid(); dto.ActualQuantity = actual is null ? null : decimal.Parse(actual);
        var result = _validator.Validate(dto); result.IsValid.Should().BeTrue(); result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SystemQuantity")]
    [InlineData("ActualQuantity")]
    public void Validate_QuantityIsNegative_Fails(string property)
    {
        var dto = Valid(); if (property == nameof(dto.SystemQuantity)) dto.SystemQuantity = -1; else dto.ActualQuantity = -1;
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == property);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validate_NoteLength_ReturnsExpectedResult(int length, bool expected)
    {
        var dto = Valid(); dto.Note = new string('a', length); var result = _validator.Validate(dto);
        result.IsValid.Should().Be(expected); if (!expected) result.Errors.Should().Contain(x => x.PropertyName == nameof(dto.Note));
    }
}

public class UpdateStockTakeItemDtoValidatorTests
{
    private readonly UpdateStockTakeItemDtoValidator _validator = new();
    private static UpdateStockTakeItemDto Valid() => new() { Id = 0, SystemQuantity = 0, ActualQuantity = null, Note = "OK" };

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("1")]
    public void Validate_ValidDto_Passes(string? actual)
    {
        var dto = Valid(); dto.ActualQuantity = actual is null ? null : decimal.Parse(actual);
        var result = _validator.Validate(dto); result.IsValid.Should().BeTrue(); result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("SystemQuantity")]
    [InlineData("ActualQuantity")]
    public void Validate_QuantityIsNegative_Fails(string property)
    {
        var dto = Valid(); if (property == nameof(dto.SystemQuantity)) dto.SystemQuantity = -1; else dto.ActualQuantity = -1;
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == property);
    }

    [Theory]
    [InlineData(500, true)]
    [InlineData(501, false)]
    public void Validate_NoteLength_ReturnsExpectedResult(int length, bool expected)
    {
        var dto = Valid(); dto.Note = new string('a', length); var result = _validator.Validate(dto);
        result.IsValid.Should().Be(expected); if (!expected) result.Errors.Should().Contain(x => x.PropertyName == nameof(dto.Note));
    }
}
