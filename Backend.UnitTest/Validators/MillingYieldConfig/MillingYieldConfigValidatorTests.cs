using Backend.Application.DTOs.MillingYieldConfigs;
using Backend.Application.Validators.MillingYieldConfigs;
using FluentAssertions;

namespace Backend.UnitTest.Validators.MillingYieldConfig;

public class CreateMillingYieldConfigDtoValidatorTests
{
    private readonly CreateMillingYieldConfigDtoValidator _validator=new();
    private static CreateMillingYieldConfigDto Valid()=>new(){YieldRate=1,MoistureFrom=null,MoistureTo=null,BrokenRiceRate=null,BranRate=null,HuskRate=null};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("0",false)] [InlineData("0.1",true)] [InlineData("1",true)] [InlineData("1.1",false)] public void Validate_YieldRateBoundary_ReturnsExpectedResult(string text,bool expected){var d=Valid();d.YieldRate=decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.YieldRate));}
    [Theory]
    [InlineData("MoistureFrom",null,true)] [InlineData("MoistureFrom","0",true)] [InlineData("MoistureFrom","100",true)] [InlineData("MoistureFrom","-1",false)] [InlineData("MoistureFrom","101",false)]
    [InlineData("MoistureTo",null,true)] [InlineData("MoistureTo","0",true)] [InlineData("MoistureTo","100",true)] [InlineData("MoistureTo","-1",false)] [InlineData("MoistureTo","101",false)]
    public void Validate_MoistureBoundary_ReturnsExpectedResult(string p,string? text,bool expected){var d=Valid();var v=text is null?(decimal?)null:decimal.Parse(text);if(p==nameof(d.MoistureFrom))d.MoistureFrom=v;else d.MoistureTo=v;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("10","10",true)] [InlineData("10","9",false)] [InlineData(null,"9",true)] [InlineData("10",null,true)] public void Validate_MoistureRange_ReturnsExpectedResult(string? from,string? to,bool expected){var d=Valid();d.MoistureFrom=from is null?null:decimal.Parse(from);d.MoistureTo=to is null?null:decimal.Parse(to);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.MoistureTo));}
    [Theory]
    [InlineData("BrokenRiceRate",null,true)] [InlineData("BrokenRiceRate","0",true)] [InlineData("BrokenRiceRate","1",true)] [InlineData("BrokenRiceRate","-0.1",false)] [InlineData("BrokenRiceRate","1.1",false)]
    [InlineData("BranRate",null,true)] [InlineData("BranRate","0",true)] [InlineData("BranRate","1",true)] [InlineData("BranRate","-0.1",false)] [InlineData("BranRate","1.1",false)]
    [InlineData("HuskRate",null,true)] [InlineData("HuskRate","0",true)] [InlineData("HuskRate","1",true)] [InlineData("HuskRate","-0.1",false)] [InlineData("HuskRate","1.1",false)]
    public void Validate_OptionalRateBoundary_ReturnsExpectedResult(string p,string? text,bool expected){var d=Valid();var v=text is null?(decimal?)null:decimal.Parse(text);if(p==nameof(d.BrokenRiceRate))d.BrokenRiceRate=v;else if(p==nameof(d.BranRate))d.BranRate=v;else d.HuskRate=v;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
}

public class UpdateMillingYieldConfigDtoValidatorTests
{
    private readonly UpdateMillingYieldConfigDtoValidator _validator=new();
    private static UpdateMillingYieldConfigDto Valid()=>new(){Id=1,YieldRate=1};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData(1,true)] [InlineData(0,false)] [InlineData(-1,false)] public void Validate_IdBoundary_ReturnsExpectedResult(int id,bool expected){var d=Valid();d.Id=id;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Id));}
    [Theory] [InlineData("0",false)] [InlineData("0.1",true)] [InlineData("1",true)] [InlineData("1.1",false)] public void Validate_YieldRateBoundary_ReturnsExpectedResult(string text,bool expected){var d=Valid();d.YieldRate=decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.YieldRate));}
    [Theory]
    [InlineData("MoistureFrom",null,true)] [InlineData("MoistureFrom","0",true)] [InlineData("MoistureFrom","100",true)] [InlineData("MoistureFrom","-1",false)] [InlineData("MoistureFrom","101",false)]
    [InlineData("MoistureTo",null,true)] [InlineData("MoistureTo","0",true)] [InlineData("MoistureTo","100",true)] [InlineData("MoistureTo","-1",false)] [InlineData("MoistureTo","101",false)]
    public void Validate_MoistureBoundary_ReturnsExpectedResult(string p,string? text,bool expected){var d=Valid();var v=text is null?(decimal?)null:decimal.Parse(text);if(p==nameof(d.MoistureFrom))d.MoistureFrom=v;else d.MoistureTo=v;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("10","10",true)] [InlineData("10","9",false)] [InlineData(null,"9",true)] [InlineData("10",null,true)] public void Validate_MoistureRange_ReturnsExpectedResult(string? from,string? to,bool expected){var d=Valid();d.MoistureFrom=from is null?null:decimal.Parse(from);d.MoistureTo=to is null?null:decimal.Parse(to);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.MoistureTo));}
    [Theory]
    [InlineData("BrokenRiceRate",null,true)] [InlineData("BrokenRiceRate","0",true)] [InlineData("BrokenRiceRate","1",true)] [InlineData("BrokenRiceRate","-0.1",false)] [InlineData("BrokenRiceRate","1.1",false)]
    [InlineData("BranRate",null,true)] [InlineData("BranRate","0",true)] [InlineData("BranRate","1",true)] [InlineData("BranRate","-0.1",false)] [InlineData("BranRate","1.1",false)]
    [InlineData("HuskRate",null,true)] [InlineData("HuskRate","0",true)] [InlineData("HuskRate","1",true)] [InlineData("HuskRate","-0.1",false)] [InlineData("HuskRate","1.1",false)]
    public void Validate_OptionalRateBoundary_ReturnsExpectedResult(string p,string? text,bool expected){var d=Valid();var v=text is null?(decimal?)null:decimal.Parse(text);if(p==nameof(d.BrokenRiceRate))d.BrokenRiceRate=v;else if(p==nameof(d.BranRate))d.BranRate=v;else d.HuskRate=v;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
}
