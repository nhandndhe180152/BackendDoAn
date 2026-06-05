using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.FolderUploads;
using FluentValidation;

namespace Backend.Application.Validators.FolderUploads;

public class CreateFolderUploadDtoValidator : AbstractValidator<CreateFolderUploadDto>
{
    public CreateFolderUploadDtoValidator()
    {
        RuleFor(x => x.FolderName)
            .NotNull()
            .WithName("Tên thư mục")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên thư mục")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên thư mục")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
