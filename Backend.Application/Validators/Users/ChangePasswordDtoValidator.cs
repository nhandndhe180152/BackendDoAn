using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Users;
using FluentValidation;

namespace Backend.Application.Validators.Users;

public class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.NewPassword)
           .MinimumLength(10)
           .WithName("Mật khẩu mới")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MinLengthMessage));
    }
}