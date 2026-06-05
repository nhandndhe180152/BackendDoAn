using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotWeights;
using FluentValidation;

namespace Backend.Application.Validators.IotWeights;

public class ReceiveIotWeightDtoValidator : AbstractValidator<ReceiveIotWeightDto>
{
    public ReceiveIotWeightDtoValidator()
    {
        RuleFor(x => x.DeviceCode)
            .NotEmpty()
            .WithName("Mã thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(100)
            .WithName("Mã thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x)
            .Must(x => x.WeightKg.HasValue || x.Weight.HasValue)
            .WithName("Khối lượng")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.WeightKg)
            .GreaterThanOrEqualTo(-0.05m)
            .When(x => x.WeightKg.HasValue)
            .WithName("Khối lượng")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(-0.05m)
            .When(x => x.Weight.HasValue)
            .WithName("Khối lượng")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.Unit)
            .MaximumLength(10)
            .When(x => !string.IsNullOrWhiteSpace(x.Unit))
            .WithName("Đơn vị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ReferenceType)
            .MaximumLength(50)
            .When(x => !string.IsNullOrWhiteSpace(x.ReferenceType))
            .WithName("Loại chứng từ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
