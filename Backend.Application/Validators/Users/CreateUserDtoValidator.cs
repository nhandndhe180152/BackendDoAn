using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Users;
using Backend.Share.Enums;
using Backend.Share.Helpers;
using FluentValidation;

namespace Backend.Application.Validators.Users;

public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        // Lưu ý: KHÔNG validate mật khẩu ở đây. Mật khẩu do hệ thống tự sinh khi admin tạo
        // tài khoản (bàn giao qua email + bắt đổi lần đầu) nên bỏ để tăng tốc thao tác tạo.

        RuleFor(x => x.Username)
            .NotNull()
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MinimumLength(6)
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MinLengthMessage))
            .MaximumLength(30)
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .Must(username => string.IsNullOrEmpty(username) || StringHelper.IsValidUsername(username))
            .WithName("Tên đăng nhập")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage));

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
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage))
            .EmailAddress()
            .WithName("Email")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage));

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

        // Bắt buộc chọn ít nhất một vai trò để tài khoản có quyền sử dụng.
        RuleFor(x => x.Roles)
            .NotNull()
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.RequiredRole))
            .Must(roles => roles != null && roles.Count > 0)
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.RequiredRole));
    }
}
