using Backend.Application.DTOs.InventoryTransactions;
using Backend.Application.Validators.InventoryTransactions;
using FluentAssertions;

namespace Backend.UnitTest.Validators.Inventory;

public class ManualInventoryAdjustmentDtoValidatorTests
{
    private readonly ManualInventoryAdjustmentDtoValidator _validator=new();
    private static ManualInventoryAdjustmentDto Valid()=>new(){ProductVariantId=1,WarehouseId=1,NewQuantityOnHand=0,AdjustmentQuantity=null,Reason="Reason"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("ProductVariantId",0)] [InlineData("ProductVariantId",-1)] [InlineData("WarehouseId",0)] [InlineData("WarehouseId",-1)] public void Validate_IdIsNotPositive_Fails(string p,int v){var d=Valid();if(p==nameof(d.ProductVariantId))d.ProductVariantId=v;else d.WarehouseId=v;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Fact] public void Validate_BothQuantityOptionsAreNull_FailsObjectRule(){var d=Valid();d.NewQuantityOnHand=null;d.AdjustmentQuantity=null;var r=_validator.Validate(d);r.IsValid.Should().BeFalse();r.Errors.Should().Contain(x=>x.PropertyName==string.Empty);}
    [Theory] [InlineData("0",true)] [InlineData("-1",false)] public void Validate_NewQuantityOnHandBoundary_ReturnsExpectedResult(string text,bool expected){var d=Valid();d.NewQuantityOnHand=decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.NewQuantityOnHand));}
    [Theory] [InlineData("1",true)] [InlineData("-1",true)] [InlineData("0",false)] public void Validate_AdjustmentQuantityNotEqualZero_ReturnsExpectedResult(string text,bool expected){var d=Valid();d.NewQuantityOnHand=null;d.AdjustmentQuantity=decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.AdjustmentQuantity));}
    [Theory] [InlineData(null)] [InlineData("")] [InlineData("   ")] public void Validate_ReasonIsMissing_Fails(string? value){var d=Valid();d.Reason=value!;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==nameof(d.Reason));}
    [Theory] [InlineData(500,true)] [InlineData(501,false)] public void Validate_ReasonLength_ReturnsExpectedResult(int n,bool expected){var d=Valid();d.Reason=new string('a',n);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Reason));}
}

public class StockMovementRequestDtoValidatorTests
{
    private readonly StockMovementRequestDtoValidator _validator=new();
    private static StockMovementRequestDto Valid()=>new(){ProductVariantId=1,WarehouseId=1,Quantity=1,Note="Note"};
    [Fact] public void Validate_ValidDto_Passes(){var r=_validator.Validate(Valid());r.IsValid.Should().BeTrue();r.Errors.Should().BeEmpty();}
    [Theory] [InlineData("ProductVariantId",0)] [InlineData("ProductVariantId",-1)] [InlineData("WarehouseId",0)] [InlineData("WarehouseId",-1)] public void Validate_IdIsNotPositive_Fails(string p,int v){var d=Valid();if(p==nameof(d.ProductVariantId))d.ProductVariantId=v;else d.WarehouseId=v;_validator.Validate(d).Errors.Should().Contain(x=>x.PropertyName==p);}
    [Theory] [InlineData("1",true)] [InlineData("0",false)] [InlineData("-1",false)] public void Validate_QuantityBoundary_ReturnsExpectedResult(string text,bool expected){var d=Valid();d.Quantity=decimal.Parse(text);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Quantity));}
    [Theory] [InlineData(500,true)] [InlineData(501,false)] public void Validate_NoteLength_ReturnsExpectedResult(int n,bool expected){var d=Valid();d.Note=new string('a',n);var r=_validator.Validate(d);r.IsValid.Should().Be(expected);if(!expected)r.Errors.Should().Contain(x=>x.PropertyName==nameof(d.Note));}
}
