using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Users;
using Backend.Share.Enums;
using Backend.Share.Helpers;
using FluentValidation;

namespace Backend.Application.Validators.Users;

public class UpdateProfileDtoValidator : AbstractValidator<UpdateUserProfileDto>
{
    public UpdateProfileDtoValidator()
    {
        RuleFor(x => x.FirstName)
            .NotNull()
            .WithName("Họ và tên đệm")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Họ và tên đệm")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Họ và tên đệm")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.LastName)
            .NotNull()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(50)
            .WithName("số điện thoại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Must(phoneNumber => string.IsNullOrEmpty(phoneNumber) || PhoneHelper.IsValidVietnamPhone(phoneNumber))
            .WithName("Số điện thoại")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage));

        RuleFor(x => x.Gender)
            .Must(gender => gender == null || gender == (int)Gender.Male || gender == (int)Gender.Female)
            .WithName("Giới tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData));

        RuleFor(x => x.IdentityNumber)
            .Must(identityNumber => string.IsNullOrEmpty(identityNumber) || StringHelper.IsValidIdentityNumber(identityNumber))
            .WithName("CCCD/CMND")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage));
    }
}
