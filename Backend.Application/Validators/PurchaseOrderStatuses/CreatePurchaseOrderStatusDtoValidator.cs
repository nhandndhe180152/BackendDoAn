using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.PurchaseOrderStatuses;
using FluentValidation;

namespace Backend.Application.Validators.PurchaseOrderStatuses;

public class CreatePurchaseOrderStatusDtoValidator : AbstractValidator<CreatePurchaseOrderStatusDto>
{
    public CreatePurchaseOrderStatusDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .MaximumLength(100)
            .Matches("^[A-Za-z][A-Za-z0-9_]*$")
            .WithMessage("Mã trạng thái chỉ được chứa chữ cái, chữ số và dấu gạch dưới, đồng thời phải bắt đầu bằng chữ cái.");

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
