using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using FluentValidation;

namespace Backend.Application.Validators.IotDeviceCommands;

public class CompleteIotDeviceCommandDtoValidator : AbstractValidator<CompleteIotDeviceCommandDto>
{
    public CompleteIotDeviceCommandDtoValidator()
    {
        RuleFor(x => x.ResultMessage)
            .MaximumLength(500)
            .WithName("Kết quả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
