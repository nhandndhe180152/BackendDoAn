using Backend.Application.DTOs.PaddyPurchaseSchedules;
using Backend.Application.Validators.PaddyPurchaseSchedules;
using FluentAssertions;

namespace Backend.UnitTest.Validators.PaddyPurchaseSchedule;

public class CreatePaddyPurchaseScheduleDtoValidatorTests
{
    private readonly CreatePaddyPurchaseScheduleDtoValidator _validator=new();
    [Theory] [InlineData(null,true)] [InlineData(1,true)] [InlineData(0,false)] [InlineData(-1,false)] public void Validate_WarehouseId_ReturnsExpectedResult(int? value,bool expected){var d=new CreatePaddyPurchaseScheduleDto{WarehouseId=value};var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.WarehouseId));else r.Errors.Should().BeEmpty();}
}
public class UpdatePaddyPurchaseScheduleDtoValidatorTests
{
    private readonly UpdatePaddyPurchaseScheduleDtoValidator _validator=new();
    [Theory] [InlineData(null,true)] [InlineData(1,true)] [InlineData(0,false)] [InlineData(-1,false)] public void Validate_WarehouseId_ReturnsExpectedResult(int? value,bool expected){var d=new UpdatePaddyPurchaseScheduleDto{WarehouseId=value};var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.WarehouseId));else r.Errors.Should().BeEmpty();}
}
