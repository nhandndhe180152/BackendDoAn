using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Farmers;
using FluentValidation;

namespace Backend.Application.Validators.Farmers;

public class UpdateFarmerDtoValidator : AbstractValidator<UpdateFarmerDto>
{
    public UpdateFarmerDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithName("Mã nông dân")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Mã nông dân")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithName("Tên nông dân")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên nông dân")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .WithName("Số điện thoại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .WithName("Địa chỉ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Region)
            .MaximumLength(255)
            .WithName("Khu vực")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ReputationNote)
            .MaximumLength(500)
            .WithName("Ghi chú uy tín")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
