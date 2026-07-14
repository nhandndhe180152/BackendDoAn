using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Organizations;
using FluentValidation;

namespace Backend.Application.Validators.Organizations;

public class UpdateOrganizationDtoValidator : AbstractValidator<UpdateOrganizationDto>
{
    public UpdateOrganizationDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueMessage));

        RuleFor(x => x.Code)
            .NotEmpty()
            .WithName("Mã doanh nghiệp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Mã doanh nghiệp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Name)
            .NotEmpty()
            .WithName("Tên doanh nghiệp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên doanh nghiệp")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.TaxCode)
            .MaximumLength(50)
            .WithName("Mã số thuế")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Address)
            .MaximumLength(500)
            .WithName("Địa chỉ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ContactEmail)
            .MaximumLength(255)
            .WithName("Email liên hệ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .EmailAddress()
            .When(x => !string.IsNullOrWhiteSpace(x.ContactEmail))
            .WithMessage("Email không đúng định dạng.");

        RuleFor(x => x.ContactPhone)
            .MaximumLength(50)
            .WithName("Số điện thoại liên hệ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
