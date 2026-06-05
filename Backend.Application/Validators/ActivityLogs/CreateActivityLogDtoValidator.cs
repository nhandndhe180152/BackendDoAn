using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.ActivityLogs;
using FluentValidation;

namespace Backend.Application.Validators.ActivityLogs;

public class CreateActivityLogDtoValidator : AbstractValidator<CreateActivityLogDto>
{
    public CreateActivityLogDtoValidator()
    {
        RuleFor(x => x.Action)
            .MaximumLength(255)
            .WithName("Thao tác")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
