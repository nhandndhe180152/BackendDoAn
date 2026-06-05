using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using FluentValidation;

namespace Backend.Application.Validators.IotDeviceCommands;

public class CreateIotDeviceCommandDtoValidator : AbstractValidator<CreateIotDeviceCommandDto>
{
    public CreateIotDeviceCommandDtoValidator()
    {
        RuleFor(x => x.IotDeviceId)
            .GreaterThan(0)
            .WithName("Thiết bị IoT")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.CommandType)
            .NotEmpty()
            .WithName("Loại lệnh")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Loại lệnh")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Must(x => IotDeviceCommandConstants.AllowedCommandTypes.Contains(x.Trim().ToUpperInvariant()))
            .WithName("Loại lệnh")
            .WithMessage("Loại lệnh chỉ được là TARE, RESET hoặc CALIBRATE.");

        RuleFor(x => x.Payload)
            .MaximumLength(2000)
            .WithName("Payload")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ExpiredAt)
            .Must(x => x == null || x > DateTime.Now)
            .WithName("Thời gian hết hạn")
            .WithMessage("Thời gian hết hạn phải lớn hơn thời điểm hiện tại.");
    }
}
