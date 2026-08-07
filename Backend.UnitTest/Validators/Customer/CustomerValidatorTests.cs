using Backend.Application.DTOs.Customers;
using Backend.Application.Validators.Customers;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Customer;

public class CreateCustomerDtoValidatorTests
{
    private readonly CreateCustomerDtoValidator _validator=new();
    private static CreateCustomerDto Valid()=>new(){Code="CUS",Name="Customer",CustomerType="Retail",ContactPerson="Person",Phone="123",Email="a@b.com",Address="A",TaxCode="T"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("Code",null)] [InlineData("Code","")] [InlineData("Code","   ")] [InlineData("Name",null)] [InlineData("Name","")] [InlineData("Name","   ")] public void Validate_RequiredTextIsMissing_Fails(string p,string? v){var d=Valid();if(p==nameof(d.Code))d.Code=v!;else d.Name=v!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("Code",50,true)] [InlineData("Code",51,false)] [InlineData("Name",255,true)] [InlineData("Name",256,false)] [InlineData("CustomerType",50,true)] [InlineData("CustomerType",51,false)] [InlineData("ContactPerson",255,true)] [InlineData("ContactPerson",256,false)] [InlineData("Phone",50,true)] [InlineData("Phone",51,false)] [InlineData("Address",500,true)] [InlineData("Address",501,false)] [InlineData("TaxCode",50,true)] [InlineData("TaxCode",51,false)] public void Validate_TextLength_ReturnsExpectedResult(string p,int n,bool expected){var d=Valid();var v=new string('a',n);switch(p){case "Code":d.Code=v;break;case "Name":d.Name=v;break;case "CustomerType":d.CustomerType=v;break;case "ContactPerson":d.ContactPerson=v;break;case "Phone":d.Phone=v;break;case "Address":d.Address=v;break;default:d.TaxCode=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData(null,true)] [InlineData("",true)] [InlineData("   ",true)] [InlineData("valid@example.com",true)] [InlineData("invalid",false)] public void Validate_EmailConditionAndFormat_ReturnsExpectedResult(string? email,bool expected){var d=Valid();d.Email=email;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Email));}
    [Theory] [InlineData(255,true)] [InlineData(256,false)] public void Validate_EmailLength_ReturnsExpectedResult(int n,bool expected){var d=Valid();d.Email=new string('a',n-6)+"@b.com";var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Email));}
}

public class UpdateCustomerDtoValidatorTests
{
    private readonly UpdateCustomerDtoValidator _validator=new();
    private static UpdateCustomerDto Valid()=>new(){Id=1,Code="CUS",Name="Customer",CustomerType="Retail",ContactPerson="Person",Phone="123",Email="a@b.com",Address="A",TaxCode="T"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData(1,true)] [InlineData(0,false)] [InlineData(-1,false)] public void Validate_IdBoundary_ReturnsExpectedResult(int id,bool expected){var d=Valid();d.Id=id;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Id));}
    [Theory] [InlineData("Code",null)] [InlineData("Code","")] [InlineData("Code","   ")] [InlineData("Name",null)] [InlineData("Name","")] [InlineData("Name","   ")] public void Validate_RequiredTextIsMissing_Fails(string p,string? v){var d=Valid();if(p==nameof(d.Code))d.Code=v!;else d.Name=v!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("Code",50,true)] [InlineData("Code",51,false)] [InlineData("Name",255,true)] [InlineData("Name",256,false)] [InlineData("CustomerType",50,true)] [InlineData("CustomerType",51,false)] [InlineData("ContactPerson",255,true)] [InlineData("ContactPerson",256,false)] [InlineData("Phone",50,true)] [InlineData("Phone",51,false)] [InlineData("Address",500,true)] [InlineData("Address",501,false)] [InlineData("TaxCode",50,true)] [InlineData("TaxCode",51,false)] public void Validate_TextLength_ReturnsExpectedResult(string p,int n,bool expected){var d=Valid();var v=new string('a',n);switch(p){case "Code":d.Code=v;break;case "Name":d.Name=v;break;case "CustomerType":d.CustomerType=v;break;case "ContactPerson":d.ContactPerson=v;break;case "Phone":d.Phone=v;break;case "Address":d.Address=v;break;default:d.TaxCode=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData(null,true)] [InlineData("",true)] [InlineData("   ",true)] [InlineData("valid@example.com",true)] [InlineData("invalid",false)] public void Validate_EmailConditionAndFormat_ReturnsExpectedResult(string? email,bool expected){var d=Valid();d.Email=email;var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Email));}
    [Theory] [InlineData(255,true)] [InlineData(256,false)] public void Validate_EmailLength_ReturnsExpectedResult(int n,bool expected){var d=Valid();d.Email=new string('a',n-6)+"@b.com";var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Email));}
}
