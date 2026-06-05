using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Wards;
using FluentValidation;

namespace Backend.Application.Validators.Wards;

public class UpdateWardDtoValidator : AbstractValidator<CreateWardDto>
{
    public UpdateWardDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotNull()
            .WithName("Tên Xã")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(255)
           .WithName("Tên Xã")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Code)
            .MaximumLength(255)
            .WithName("Mã Xã")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Slug)
            .MaximumLength(255)
            .WithName("Tên viết tắt")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Type)
            .MaximumLength(255)
            .WithName("Loại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ProvinceId)
            .NotNull()
            .WithName("Tỉnh/Thành phố")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
    }
}
