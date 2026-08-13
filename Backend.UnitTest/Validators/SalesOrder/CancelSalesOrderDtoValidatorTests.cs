using Backend.Application.DTOs.SalesOrders;
using Backend.Application.Validators.SalesOrders;
using FluentAssertions;

namespace Backend.UnitTest.Validators.SalesOrder;

public class CancelSalesOrderDtoValidatorTests
{
    private readonly CancelSalesOrderDtoValidator _validator = new();
    private static CancelSalesOrderDto Valid() => new() { Reason = "Khách hủy đặt hàng" };

    [Fact]
    public void Validate_ValidDto_Passes()
    {
        var result = _validator.Validate(Valid());
        result.IsValid.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_ReasonIsEmpty_Fails(string? value)
    {
        var dto = Valid();
        dto.Reason = value!;

        var result = _validator.Validate(dto);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(x => x.PropertyName == nameof(dto.Reason));
    }

    [Fact]
    public void Validate_ReasonAtMaximumLength_Passes()
    {
        var dto = Valid();
        dto.Reason = new string('a', 500);

        _validator.Validate(dto).IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_ReasonExceedsMaximumLength_Fails()
    {
        var dto = Valid();
        dto.Reason = new string('a', 501);

        _validator.Validate(dto).Errors.Should().Contain(x => x.PropertyName == nameof(dto.Reason));
    }
}
