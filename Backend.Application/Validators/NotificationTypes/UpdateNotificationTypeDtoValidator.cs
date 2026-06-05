using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.NotificationTypes;
using FluentValidation;

namespace Backend.Application.Validators.NotificationTypes;

public class UpdateNotificationTypeDtoValidator : AbstractValidator<UpdateNotificationTypeDto>
{
    public UpdateNotificationTypeDtoValidator()
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
    }
}