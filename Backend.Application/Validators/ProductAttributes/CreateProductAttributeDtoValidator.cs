using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductAttributes;
using FluentValidation;

namespace Backend.Application.Validators.ProductAttributes;

public class CreateProductAttributeDtoValidator : AbstractValidator<CreateProductAttributeDto>
{
    public CreateProductAttributeDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .WithName("Tên thuộc tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên thuộc tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên thuộc tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithName("Mô tả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
