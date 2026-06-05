using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Provinces;
using FluentValidation;

namespace Backend.Application.Validators.Provinces;

public class CreateProvinceDtoValidator : AbstractValidator<CreateProvinceDto>
{
    public CreateProvinceDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
           .WithName("Tên tỉnh")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(255)
           .WithName("Tên tỉnh")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Code)
            .MaximumLength(255)
            .WithName("Mã tỉnh")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Slug)
            .MaximumLength(255)
            .WithName("Tên viết tắt")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Type)
            .MaximumLength(255)
            .WithName("Loại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
