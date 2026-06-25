using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.UnitOfMeasures;
using FluentValidation;

namespace Backend.Application.Validators.UnitOfMeasures;

public class UpdateUnitOfMeasureDtoValidator : AbstractValidator<UpdateUnitOfMeasureDto>
{
    public UpdateUnitOfMeasureDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithName("Tên đơn vị tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên đơn vị tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Symbol)
            .NotEmpty()
            .WithName("Ký hiệu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Ký hiệu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
