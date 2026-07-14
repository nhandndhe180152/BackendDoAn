using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.RiceVarieties;
using FluentValidation;

namespace Backend.Application.Validators.RiceVarieties;

public class UpdateRiceVarietyDtoValidator : AbstractValidator<UpdateRiceVarietyDto>
{
    public UpdateRiceVarietyDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithName("Mã giống lúa")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Mã giống lúa")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithName("Tên giống lúa")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên giống lúa")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Season)
            .MaximumLength(100)
            .WithName("Mùa vụ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.DefaultYieldRate)
            .InclusiveBetween(0m, 1m)
            .When(x => x.DefaultYieldRate.HasValue)
            .WithMessage("Tỷ lệ thu hồi (yield) phải nằm trong khoảng 0 đến 1.");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
