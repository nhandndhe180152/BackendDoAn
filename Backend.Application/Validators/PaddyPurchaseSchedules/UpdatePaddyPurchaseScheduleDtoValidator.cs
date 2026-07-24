using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.PaddyPurchaseSchedules;
using FluentValidation;

namespace Backend.Application.Validators.PaddyPurchaseSchedules;

public class UpdatePaddyPurchaseScheduleDtoValidator : AbstractValidator<UpdatePaddyPurchaseScheduleDto>
{
    public UpdatePaddyPurchaseScheduleDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .When(x => x.WarehouseId.HasValue)
            .WithName("Mã kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
    }
}
