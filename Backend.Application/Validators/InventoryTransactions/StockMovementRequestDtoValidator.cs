using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.InventoryTransactions;
using FluentValidation;

namespace Backend.Application.Validators.InventoryTransactions;

public class StockMovementRequestDtoValidator : AbstractValidator<StockMovementRequestDto>
{
    public StockMovementRequestDtoValidator()
    {
        RuleFor(x => x.ProductVariantId)
            .GreaterThan(0)
            .WithName("SKU")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Quantity)
            .GreaterThan(0)
            .WithName("Số lượng")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
