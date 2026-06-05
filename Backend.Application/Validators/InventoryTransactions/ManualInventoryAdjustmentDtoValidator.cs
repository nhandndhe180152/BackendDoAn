using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.InventoryTransactions;
using FluentValidation;

namespace Backend.Application.Validators.InventoryTransactions;

public class ManualInventoryAdjustmentDtoValidator : AbstractValidator<ManualInventoryAdjustmentDto>
{
    public ManualInventoryAdjustmentDtoValidator()
    {
        RuleFor(x => x.ProductVariantId)
            .GreaterThan(0)
            .WithName("SKU")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x)
            .Must(x => x.NewQuantityOnHand.HasValue || x.AdjustmentQuantity.HasValue)
            .WithMessage("Cần nhập số lượng tồn mới hoặc số lượng điều chỉnh.");

        RuleFor(x => x.NewQuantityOnHand)
            .GreaterThanOrEqualTo(0)
            .When(x => x.NewQuantityOnHand.HasValue)
            .WithName("Số lượng tồn mới")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.AdjustmentQuantity)
            .NotEqual(0)
            .When(x => x.AdjustmentQuantity.HasValue)
            .WithName("Số lượng điều chỉnh")
            .WithMessage("Số lượng điều chỉnh phải khác 0.");

        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithName("Lý do")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(500)
            .WithName("Lý do")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
