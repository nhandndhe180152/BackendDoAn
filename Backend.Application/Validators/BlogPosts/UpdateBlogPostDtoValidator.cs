using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPosts;
using FluentValidation;

namespace Backend.Application.Validators.BlogPosts;

public class UpdateBlogPostDtoValidator : AbstractValidator<UpdateBlogPostDto>
{
    public UpdateBlogPostDtoValidator()
    {
        RuleFor(x => x.Title)
            .NotNull()
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(500)
            .WithName("Tiêu đề")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
        RuleFor(x => x.Content)
           .NotNull()
           .WithName("Nội dung")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Nội dung")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
        RuleFor(x => x.Description)
           .NotNull()
           .WithName("Mô tả")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Mô tả")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .MaximumLength(500)
           .WithName("Mô tả")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
        RuleFor(x => x.BlogPostCategoryId)
           .NotNull()
           .WithName("Danh mục")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Danh mục")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
        RuleFor(x => x.BlogPostLayoutId)
           .NotNull()
           .WithName("Bố cục")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Bố cục")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
        RuleFor(x => x.BlogPostStatusId)
           .NotNull()
           .WithName("Trạng thái")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
           .NotEmpty()
           .WithName("Trạng thái")
           .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));
    }
}
