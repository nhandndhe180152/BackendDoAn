using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Users;
using FluentValidation;

namespace Backend.Application.Validators.Users;

public class AdminRegisterDtoValidator : AbstractValidator<AdminRegisterDto>
{
    public AdminRegisterDtoValidator()
    {
        RuleFor(x => x.Username)
            .NotNull()
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(30)
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .MinimumLength(6)
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MinLengthMessage));

        RuleFor(x => x.Password)
            .NotNull()
            .WithName("Mật khẩu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Mật khẩu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(64)
            .WithName("Mật khẩu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .MinimumLength(8)
            .WithName("Mật khẩu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MinLengthMessage));

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

        RuleFor(x => x.Email)
            .NotNull()
            .WithName("Email")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Email")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(500)
            .WithName("Email")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
