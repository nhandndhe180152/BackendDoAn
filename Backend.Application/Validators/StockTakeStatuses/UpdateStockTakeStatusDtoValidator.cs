using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakeStatuses;
using FluentValidation;

namespace Backend.Application.Validators.StockTakeStatuses;

public class UpdateStockTakeStatusDtoValidator : AbstractValidator<UpdateStockTakeStatusDto>
{
    public UpdateStockTakeStatusDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
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
        RuleFor(x => x.Code)
            .MaximumLength(100)
            .WithName("Mã")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
