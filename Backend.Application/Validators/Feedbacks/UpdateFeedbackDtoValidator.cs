using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Feedbacks;
using FluentValidation;

namespace Backend.Application.Validators.Feedbacks;

public class UpdateFeedbackDtoValidator : AbstractValidator<UpdateFeedbackDto>
{
    public UpdateFeedbackDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotNull()
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(500)
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Content)
            .NotNull()
            .WithName("Nội dung")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Nội dung")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(1000)
            .WithName("Nội dung")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
