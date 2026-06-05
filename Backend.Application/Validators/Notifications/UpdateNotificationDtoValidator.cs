using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Notifications;
using FluentValidation;

namespace Backend.Application.Validators.Notifications;

public class UpdateNotificationDtoValidator : AbstractValidator<UpdateNotificationDto>
{
    public UpdateNotificationDtoValidator()
    {
        RuleFor(x => x.Title)
           .NotNull()
           .WithName("Tiêu đề")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Tiêu đề")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(255)
           .WithName("Tên")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Content)
           .NotNull()
           .WithName("Nội dung")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Nội dung")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(500)
           .WithName("Nội dung")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.NotificationCategoryId)
          .NotNull()
          .WithName("Danh mục thông báo")
          .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
          .NotEmpty()
          .WithName("Danh mục thông báo")
          .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.DirectionId)
           .MaximumLength(255)
           .WithName("Mã đến")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
