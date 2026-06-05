using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotDevices;
using FluentValidation;

namespace Backend.Application.Validators.IotDevices;

public class CreateIotDeviceDtoValidator : AbstractValidator<CreateIotDeviceDto>
{
    public CreateIotDeviceDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.DeviceName)
            .NotEmpty()
            .WithName("Tên thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.DeviceCode)
            .NotEmpty()
            .WithName("Mã thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(100)
            .WithName("Mã thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Matches("^[A-Za-z0-9_-]+$")
            .WithName("Mã thiết bị")
            .WithMessage("Mã thiết bị chỉ được chứa chữ cái, số, dấu gạch ngang hoặc gạch dưới.");

        RuleFor(x => x.DeviceType)
            .NotEmpty()
            .WithName("Loại thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(100)
            .WithName("Loại thiết bị")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Location)
            .MaximumLength(255)
            .WithName("Vị trí")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.MqttTopic)
            .MaximumLength(500)
            .WithName("MQTT Topic")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ApiKey)
            .MinimumLength(8)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiKey))
            .WithName("Device Key")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MinLengthMessage))
            .MaximumLength(255)
            .When(x => !string.IsNullOrWhiteSpace(x.ApiKey))
            .WithName("Device Key")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
