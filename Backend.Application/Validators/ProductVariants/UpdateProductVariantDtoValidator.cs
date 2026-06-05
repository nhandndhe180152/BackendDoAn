using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductVariants;
using FluentValidation;

namespace Backend.Application.Validators.ProductVariants;

public class UpdateProductVariantDtoValidator : AbstractValidator<UpdateProductVariantDto>
{
    public UpdateProductVariantDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.Name)
            .NotNull()
            .WithName("Tên biến thể")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Tên biến thể")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(255)
            .WithName("Tên biến thể")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithName("Mô tả")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.ProductId)
            .NotEmpty()
            .WithName("Sản phẩm")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.UnitOfMeasureId)
            .NotEmpty()
            .WithName("Đơn vị tính")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.SKU)
            .NotNull()
            .WithName("Mã SKU")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .NotEmpty()
            .WithName("Mã SKU")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(50)
            .WithName("Mã SKU")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.QRCode)
            .MaximumLength(255)
            .WithName("Mã QR")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));

        RuleFor(x => x.CostPrice)
            .GreaterThanOrEqualTo(0)
            .WithName("Giá vốn")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.SalePrice)
            .GreaterThanOrEqualTo(0)
            .WithName("Giá bán")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.Weight)
            .GreaterThanOrEqualTo(0)
            .WithName("Khối lượng")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage));

        RuleFor(x => x.MinStockLevel)
            .GreaterThanOrEqualTo(0)
            .WithName("Mức tồn kho tối thiểu")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.GreaterThanValueOrEqualMessage))
            .When(x => x.MinStockLevel.HasValue);
    }
}
