using Backend.Application.DTOs.CustomerReturns;
using FluentValidation;

namespace Backend.Application.Validators.CustomerReturns;

public sealed class CreateCustomerReturnOrderDtoValidator : AbstractValidator<CreateCustomerReturnOrderDto>
{
    public CreateCustomerReturnOrderDtoValidator()
    {
        RuleFor(x => x.WarehouseId).GreaterThan(0);
        RuleFor(x => x.CustomerId).GreaterThan(0);
        RuleFor(x => x.OutboundOrderId).NotNull().GreaterThan(0);
        RuleFor(x => x.ReturnReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new CreateCustomerReturnOrderItemDtoValidator());
    }
}

public sealed class UpdateCustomerReturnOrderDtoValidator : AbstractValidator<UpdateCustomerReturnOrderDto>
{
    public UpdateCustomerReturnOrderDtoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.ReturnReason).NotEmpty().MaximumLength(500);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).SetValidator(new CreateCustomerReturnOrderItemDtoValidator());
    }
}

public sealed class CreateCustomerReturnOrderItemDtoValidator : AbstractValidator<CreateCustomerReturnOrderItemDto>
{
    public CreateCustomerReturnOrderItemDtoValidator()
    {
        RuleFor(x => x.OutboundOrderItemId).NotNull().GreaterThan(0);
        RuleFor(x => x.ProductVariantId).GreaterThan(0);
        RuleFor(x => x.QuantityReturned).GreaterThan(0);
        RuleFor(x => x.Allocations).NotEmpty();
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.OutboundOrderItemAllocationId).NotNull().GreaterThan(0);
            a.RuleFor(x => x.QuantityReturned).GreaterThan(0);
        });
        RuleFor(x => x).Must(x => Math.Abs(x.Allocations.Sum(a => a.QuantityReturned) - x.QuantityReturned) <= 0.001m)
            .WithMessage("Tổng số lượng theo lô phải bằng số lượng trả của dòng hàng.");
    }
}

public sealed class ReceiveCustomerReturnOrderDtoValidator : AbstractValidator<ReceiveCustomerReturnOrderDto>
{
    public ReceiveCustomerReturnOrderDtoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Note).MaximumLength(1000);
        RuleFor(x => x.CarrierReference).MaximumLength(100);
        RuleFor(x => x.Allocations).NotEmpty();
        RuleForEach(x => x.Allocations).ChildRules(a =>
        {
            a.RuleFor(x => x.ReturnAllocationId).GreaterThan(0);
            a.RuleFor(x => x.QuantityReceived).GreaterThanOrEqualTo(0);
            a.RuleFor(x => x.Note).MaximumLength(500);
        });
    }
}

public sealed class InspectCustomerReturnOrderDtoValidator : AbstractValidator<InspectCustomerReturnOrderDto>
{
    public InspectCustomerReturnOrderDtoValidator()
    {
        RuleFor(x => x.Id).GreaterThan(0);
        RuleFor(x => x.Items).NotEmpty();
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.CustomerReturnOrderItemId).GreaterThan(0);
            item.RuleFor(x => x.QualityStatus).Must(x => x is "GOOD" or "DAMAGED" or "EXPIRED" or "MIXED");
            item.RuleFor(x => x.Allocations).NotEmpty();
            item.RuleForEach(x => x.Allocations).ChildRules(a =>
            {
                a.RuleFor(x => x.ReturnAllocationId).GreaterThan(0);
                a.RuleFor(x => x.QuantityGood).GreaterThanOrEqualTo(0);
                a.RuleFor(x => x.QuantityDamaged).GreaterThanOrEqualTo(0);
                a.RuleFor(x => x.QuantityRejected).GreaterThanOrEqualTo(0);
                a.RuleFor(x => x.CreditQuantity).GreaterThanOrEqualTo(0);
            });
        });
    }
}

public sealed class RegisterCustomerReturnRefundDtoValidator : AbstractValidator<RegisterCustomerReturnRefundDto>
{
    public RegisterCustomerReturnRefundDtoValidator()
    {
        RuleFor(x => x.Amount).GreaterThan(0);
        RuleFor(x => x.PaymentReference).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Note).MaximumLength(300);
    }
}
