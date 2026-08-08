using Backend.Application.DTOs.Warehouses;
using Backend.Application.Validators.Warehouses;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Warehouse;

public class CreateWarehouseDtoValidatorTests
{
    private readonly CreateWarehouseDtoValidator _validator = new();
    private static CreateWarehouseDto Valid() => new() { Code = "WH", Name = "Warehouse", Address = "A", Description = "D" };

    [Fact] public void Validate_ValidDto_Passes() { var r = _validator.Validate(Valid()); r.IsValid.Should().BeTrue(); r.Errors.Should().BeEmpty(); }

    [Theory]
    [InlineData("Code", null)] [InlineData("Code", "")] [InlineData("Code", "   ")]
    [InlineData("Name", null)] [InlineData("Name", "")] [InlineData("Name", "   ")]
    public void Validate_RequiredTextIsMissing_Fails(string property, string? value)
    {
        var dto = Valid(); if (property == nameof(dto.Code)) dto.Code = value!; else dto.Name = value!;
        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == property);
    }

    [Theory]
    [InlineData("Code", 100, true)] [InlineData("Code", 101, false)]
    [InlineData("Name", 255, true)] [InlineData("Name", 256, false)]
    [InlineData("Address", 500, true)] [InlineData("Address", 501, false)]
    [InlineData("Description", 500, true)] [InlineData("Description", 501, false)]
    public void Validate_TextLength_ReturnsExpectedResult(string property, int length, bool expected)
    {
        var dto = Valid(); var value = new string('a', length);
        switch (property) { case "Code": dto.Code=value; break; case "Name": dto.Name=value; break; case "Address": dto.Address=value; break; default: dto.Description=value; break; }
        var r = _validator.Validate(dto); r.IsValid.Should().Be(expected); if (!expected) r.Errors.Should().Contain(x => x.PropertyName == property);
    }
}

public class UpdateWarehouseDtoValidatorTests
{
    private readonly UpdateWarehouseDtoValidator _validator = new();
    private static UpdateWarehouseDto Valid() => new() { Id = 1, Code = "WH", Name = "Warehouse", Address = "A", Description = "D" };
    [Fact] public void Validate_ValidDto_Passes() { var r=_validator.Validate(Valid()); r.IsValid.Should().BeTrue(); r.Errors.Should().BeEmpty(); }
    [Theory] [InlineData(0)] [InlineData(-1)] public void Validate_IdIsNotPositive_Fails(int id) { var d=Valid(); d.Id=id; _validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==nameof(d.Id)); }
    [Theory]
    [InlineData("Code", null)] [InlineData("Code", "")] [InlineData("Code", "   ")]
    [InlineData("Name", null)] [InlineData("Name", "")] [InlineData("Name", "   ")]
    public void Validate_RequiredTextIsMissing_Fails(string property, string? value) { var d=Valid(); if(property==nameof(d.Code))d.Code=value!;else d.Name=value!; _validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==property); }
    [Theory]
    [InlineData("Code",100,true)] [InlineData("Code",101,false)] [InlineData("Name",255,true)] [InlineData("Name",256,false)]
    [InlineData("Address",500,true)] [InlineData("Address",501,false)] [InlineData("Description",500,true)] [InlineData("Description",501,false)]
    public void Validate_TextLength_ReturnsExpectedResult(string property,int length,bool expected) { var d=Valid();var v=new string('a',length);switch(property){case "Code":d.Code=v;break;case "Name":d.Name=v;break;case "Address":d.Address=v;break;default:d.Description=v;break;}var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==property); }
}
