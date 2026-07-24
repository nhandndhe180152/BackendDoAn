using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using FluentValidation;

namespace Backend.Application.Validators.PaddyPurchaseSchedules;

public class CreatePaddyPurchaseScheduleDtoValidator : AbstractValidator<CreatePaddyPurchaseScheduleDto>
{
    public CreatePaddyPurchaseScheduleDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .When(x => x.WarehouseId.HasValue)
            .WithName("Mã kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
    }
}
