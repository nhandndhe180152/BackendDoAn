using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using FluentValidation;

namespace Backend.Application.Validators.PurchaseOrderStatuses;

public class UpdatePurchaseOrderStatusDtoValidator : AbstractValidator<UpdatePurchaseOrderStatusDto>
{
    public UpdatePurchaseOrderStatusDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(100)
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
        RuleFor(x => x.Color)
            .NotNull()
            .WithName("Màu sắc")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Màu sắc")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Màu sắc")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
