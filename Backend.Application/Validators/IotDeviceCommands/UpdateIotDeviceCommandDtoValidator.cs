using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDeviceCommands;
using FluentValidation;

namespace Backend.Application.Validators.IotDeviceCommands;

public class UpdateIotDeviceCommandDtoValidator : AbstractValidator<UpdateIotDeviceCommandDto>
{
    public UpdateIotDeviceCommandDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

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

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithName("Trạng thái")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Trạng thái")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Must(x => IotDeviceCommandConstants.AllowedStatuses.Contains(x.Trim().ToUpperInvariant()))
            .WithName("Trạng thái")
            .WithMessage("Trạng thái lệnh không hợp lệ.");

        RuleFor(x => x.Payload)
            .MaximumLength(2000)
            .WithName("Payload")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ResultMessage)
            .MaximumLength(500)
            .WithName("Kết quả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
