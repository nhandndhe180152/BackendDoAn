using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotWeights;
using FluentValidation;

namespace Backend.Application.Validators.IotWeights;

public class AttachIotWeightContextDtoValidator : AbstractValidator<AttachIotWeightContextDto>
{
    public AttachIotWeightContextDtoValidator()
    {
        RuleFor(x => x.ReferenceType)
            .NotEmpty()
            .WithName("Loại chứng từ")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .Must(IotWeightReferenceTypeConstants.IsValid)
            .WithName("Loại chứng từ")
            .WithMessage("ReferenceType chỉ được là PURCHASE_ORDER, SALES_ORDER, STOCK_TAKE hoặc MANUAL.");

        RuleFor(x => x.ProductVariantId)
            .NotNull()
            .GreaterThan(0)
            .When(x => IotWeightReferenceTypeConstants.RequiresReferenceItem(x.ReferenceType))
            .WithName("SKU")
            .WithMessage("ProductVariantId phải lớn hơn 0 khi gắn cân vào phiếu nghiệp vụ.");

        RuleFor(x => x.ReferenceId)
            .NotNull()
            .GreaterThan(0)
            .When(x => IotWeightReferenceTypeConstants.RequiresReferenceItem(x.ReferenceType))
            .WithName("Mã phiếu")
            .WithMessage("ReferenceId phải lớn hơn 0 khi gắn cân vào phiếu nghiệp vụ.");

        RuleFor(x => x.ReferenceItemId)
            .NotNull()
            .GreaterThan(0)
            .When(x => IotWeightReferenceTypeConstants.RequiresReferenceItem(x.ReferenceType))
            .WithName("Mã dòng phiếu")
            .WithMessage("ReferenceItemId phải lớn hơn 0 khi gắn cân vào phiếu nghiệp vụ.");

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .When(x => !string.IsNullOrWhiteSpace(x.Note))
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
