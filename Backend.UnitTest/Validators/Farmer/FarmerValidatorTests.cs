using Backend.Application.DTOs.Farmers;
using Backend.Application.Validators.Farmers;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Farmer;

public class CreateFarmerDtoValidatorTests
{
    private readonly CreateFarmerDtoValidator _validator=new();
    private static CreateFarmerDto Valid()=>new(){Code="F",Name="Farmer",Phone="1",Address="A",Region="R",ReputationNote="N"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("Code",null)] [InlineData("Code","")] [InlineData("Code","   ")] [InlineData("Name",null)] [InlineData("Name","")] [InlineData("Name","   ")] public void Validate_RequiredTextIsMissing_Fails(string p,string? v){var d=Valid();if(p==nameof(d.Code))d.Code=v!;else d.Name=v!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("Code",50,true)] [InlineData("Code",51,false)] [InlineData("Name",255,true)] [InlineData("Name",256,false)] [InlineData("Phone",50,true)] [InlineData("Phone",51,false)] [InlineData("Address",500,true)] [InlineData("Address",501,false)] [InlineData("Region",255,true)] [InlineData("Region",256,false)] [InlineData("ReputationNote",500,true)] [InlineData("ReputationNote",501,false)] public void Validate_TextLength_ReturnsExpectedResult(string p,int n,bool expected){var d=Valid();var v=new string('a',n);switch(p){case "Code":d.Code=v;break;case "Name":d.Name=v;break;case "Phone":d.Phone=v;break;case "Address":d.Address=v;break;case "Region":d.Region=v;break;default:d.ReputationNote=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
}

public class UpdateFarmerDtoValidatorTests
{
    private readonly UpdateFarmerDtoValidator _validator=new();
    private static UpdateFarmerDto Valid()=>new(){Id=1,Code="F",Name="Farmer",Phone="1",Address="A",Region="R",ReputationNote="N"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData(1,true)] [InlineData(0,false)] [InlineData(-1,false)] public void Validate_IdBoundary_ReturnsExpectedResult(int id,bool expected){var d=Valid();d.Id=id;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Id));}
    [Theory] [InlineData("Code",null)] [InlineData("Code","")] [InlineData("Code","   ")] [InlineData("Name",null)] [InlineData("Name","")] [InlineData("Name","   ")] public void Validate_RequiredTextIsMissing_Fails(string p,string? v){var d=Valid();if(p==nameof(d.Code))d.Code=v!;else d.Name=v!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("Code",50,true)] [InlineData("Code",51,false)] [InlineData("Name",255,true)] [InlineData("Name",256,false)] [InlineData("Phone",50,true)] [InlineData("Phone",51,false)] [InlineData("Address",500,true)] [InlineData("Address",501,false)] [InlineData("Region",255,true)] [InlineData("Region",256,false)] [InlineData("ReputationNote",500,true)] [InlineData("ReputationNote",501,false)] public void Validate_TextLength_ReturnsExpectedResult(string p,int n,bool expected){var d=Valid();var v=new string('a',n);switch(p){case "Code":d.Code=v;break;case "Name":d.Name=v;break;case "Phone":d.Phone=v;break;case "Address":d.Address=v;break;case "Region":d.Region=v;break;default:d.ReputationNote=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
}
