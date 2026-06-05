using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPostLayouts;
using FluentValidation;

namespace Backend.Application.Validators.BlogLayouts;

public class UpdateBlogLayoutDtoValidator : AbstractValidator<UpdateBlogPostLayoutDto>
{
    public UpdateBlogLayoutDtoValidator()
    {
        RuleFor(x => x.Name)
           .NotNull()
           .WithName("Tên")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Tên")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(255)
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithName("Mô tả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}