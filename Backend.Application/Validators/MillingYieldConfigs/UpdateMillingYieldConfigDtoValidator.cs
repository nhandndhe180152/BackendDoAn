using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.MillingYieldConfigs;
using FluentValidation;

namespace Backend.Application.Validators.MillingYieldConfigs;

public class UpdateMillingYieldConfigDtoValidator : AbstractValidator<UpdateMillingYieldConfigDto>
{
    public UpdateMillingYieldConfigDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.YieldRate)
            .GreaterThan(0m)
            .WithMessage("Tỷ lệ thu hồi gạo (yield) phải lớn hơn 0.")
            .LessThanOrEqualTo(1m)
            .WithMessage("Tỷ lệ thu hồi gạo (yield) phải nhỏ hơn hoặc bằng 1.");

        RuleFor(x => x.MoistureFrom)
            .InclusiveBetween(0m, 100m)
            .When(x => x.MoistureFrom.HasValue)
            .WithMessage("Độ ẩm (từ) phải nằm trong khoảng 0 đến 100.");

        RuleFor(x => x.MoistureTo)
            .InclusiveBetween(0m, 100m)
            .When(x => x.MoistureTo.HasValue)
            .WithMessage("Độ ẩm (đến) phải nằm trong khoảng 0 đến 100.");

        RuleFor(x => x.MoistureTo)
            .Must((dto, moistureTo) => moistureTo!.Value >= dto.MoistureFrom!.Value)
            .When(x => x.MoistureFrom.HasValue && x.MoistureTo.HasValue)
            .WithMessage("Độ ẩm (đến) phải lớn hơn hoặc bằng độ ẩm (từ).");

        RuleFor(x => x.BrokenRiceRate)
            .InclusiveBetween(0m, 1m)
            .When(x => x.BrokenRiceRate.HasValue)
            .WithMessage("Tỷ lệ tấm phải nằm trong khoảng 0 đến 1.");

        RuleFor(x => x.BranRate)
            .InclusiveBetween(0m, 1m)
            .When(x => x.BranRate.HasValue)
            .WithMessage("Tỷ lệ cám phải nằm trong khoảng 0 đến 1.");

        RuleFor(x => x.HuskRate)
            .InclusiveBetween(0m, 1m)
            .When(x => x.HuskRate.HasValue)
            .WithMessage("Tỷ lệ trấu phải nằm trong khoảng 0 đến 1.");
    }
}
