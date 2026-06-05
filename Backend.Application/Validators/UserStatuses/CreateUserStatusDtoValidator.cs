using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.UserStatuses;
using FluentValidation;

namespace Backend.Application.Validators.UserStatuses;

public class CreateUserStatusDtoValidator : AbstractValidator<CreateUserStatusDto>
{
    public CreateUserStatusDtoValidator()
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
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithName("Mô tả")
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