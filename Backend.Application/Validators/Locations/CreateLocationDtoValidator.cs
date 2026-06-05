using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Locations;
using FluentValidation;

namespace Backend.Application.Validators.Locations;

public class CreateLocationDtoValidator : AbstractValidator<CreateLocationDto>
{
    public CreateLocationDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Mã kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.ZoneName)
            .NotEmpty()
            .WithName("Tên khu vực")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(100)
            .WithName("Tên khu vực")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ShelfRow)
            .MaximumLength(50)
            .WithName("Hàng kệ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ShelfLevel)
            .MaximumLength(50)
            .WithName("Tầng kệ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.SlotCode)
            .MaximumLength(50)
            .WithName("Mã ô")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.MaxCapacity)
            .GreaterThan(0)
            .When(x => x.MaxCapacity.HasValue)
            .WithName("Sức chứa tối đa")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithName("Mô tả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
