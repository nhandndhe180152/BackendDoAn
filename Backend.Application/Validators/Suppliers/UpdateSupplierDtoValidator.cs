using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Suppliers;
using Backend.Share.Helpers;
using FluentValidation;

namespace Backend.Application.Validators.Suppliers;

public class UpdateSupplierDtoValidator : AbstractValidator<UpdateSupplierDto>
{
    public UpdateSupplierDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithName("Tên nhà cung cấp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên nhà cung cấp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithName("Mã nhà cung cấp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Mã nhà cung cấp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ContactPerson)
            .MaximumLength(255)
            .WithName("Người liên hệ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Phone)
            .MaximumLength(50)
            .WithName("Số điện thoại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Must(phone => string.IsNullOrEmpty(phone) || PhoneHelper.IsValidVietnamPhone(phone))
            .WithName("Số điện thoại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage));

        RuleFor(x => x.Email)
            .MaximumLength(255)
            .WithName("Email")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .WithName("Địa chỉ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.TaxCode)
            .MaximumLength(50)
            .WithName("Mã số thuế")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
