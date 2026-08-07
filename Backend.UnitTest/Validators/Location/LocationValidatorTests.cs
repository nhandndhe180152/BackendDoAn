using Backend.Application.DTOs.Locations;
using Backend.Application.Validators.Locations;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Location;

public class CreateLocationDtoValidatorTests
{
    private readonly CreateLocationDtoValidator _validator=new();
    private static CreateLocationDto Valid()=>new(){WarehouseId=1,ZoneName="Zone",ShelfRow="R",ShelfLevel="L",SlotCode="S",MaxCapacity=null,Description="D"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData(0)] [InlineData(-1)] public void Validate_WarehouseIdIsNotPositive_Fails(int value){var d=Valid();d.WarehouseId=value;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==nameof(d.WarehouseId));}
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")] public void Validate_ZoneNameIsMissing_Fails(string? value){var d=Valid();d.ZoneName=value!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==nameof(d.ZoneName));}
    [Theory]
    [InlineData("ZoneName",100,true)] [InlineData("ZoneName",101,false)] [InlineData("ShelfRow",50,true)] [InlineData("ShelfRow",51,false)]
    [InlineData("ShelfLevel",50,true)] [InlineData("ShelfLevel",51,false)] [InlineData("SlotCode",50,true)] [InlineData("SlotCode",51,false)]
    [InlineData("Description",500,true)] [InlineData("Description",501,false)]
    public void Validate_TextLength_ReturnsExpectedResult(string property,int length,bool expected){var d=Valid();var v=new string('a',length);switch(property){case "ZoneName":d.ZoneName=v;break;case "ShelfRow":d.ShelfRow=v;break;case "ShelfLevel":d.ShelfLevel=v;break;case "SlotCode":d.SlotCode=v;break;default:d.Description=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==property);}
    [Theory] [InlineData(null,true)] [InlineData("1",true)] [InlineData("0",false)] [InlineData("-1",false)] public void Validate_MaxCapacity_ReturnsExpectedResult(string? text,bool expected){var d=Valid();d.MaxCapacity=text is null?null:decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.MaxCapacity));}
}

public class UpdateLocationDtoValidatorTests
{
    private readonly UpdateLocationDtoValidator _validator=new();
    private static UpdateLocationDto Valid()=>new(){Id=1,WarehouseId=1,ZoneName="Zone",ShelfRow="R",ShelfLevel="L",SlotCode="S",MaxCapacity=null,Description="D"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("Id",0)] [InlineData("Id",-1)] [InlineData("WarehouseId",0)] [InlineData("WarehouseId",-1)] public void Validate_PositiveIdRuleIsViolated_Fails(string p,int v){var d=Valid();if(p==nameof(d.Id))d.Id=v;else d.WarehouseId=v;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")] public void Validate_ZoneNameIsMissing_Fails(string? value){var d=Valid();d.ZoneName=value!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==nameof(d.ZoneName));}
    [Theory]
    [InlineData("ZoneName",100,true)] [InlineData("ZoneName",101,false)] [InlineData("ShelfRow",50,true)] [InlineData("ShelfRow",51,false)] [InlineData("ShelfLevel",50,true)] [InlineData("ShelfLevel",51,false)] [InlineData("SlotCode",50,true)] [InlineData("SlotCode",51,false)] [InlineData("Description",500,true)] [InlineData("Description",501,false)]
    public void Validate_TextLength_ReturnsExpectedResult(string p,int n,bool expected){var d=Valid();var v=new string('a',n);switch(p){case "ZoneName":d.ZoneName=v;break;case "ShelfRow":d.ShelfRow=v;break;case "ShelfLevel":d.ShelfLevel=v;break;case "SlotCode":d.SlotCode=v;break;default:d.Description=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData(null,true)] [InlineData("1",true)] [InlineData("0",false)] [InlineData("-1",false)] public void Validate_MaxCapacity_ReturnsExpectedResult(string? text,bool expected){var d=Valid();d.MaxCapacity=text is null?null:decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.MaxCapacity));}
}
