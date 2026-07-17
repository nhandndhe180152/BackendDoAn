using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockAlertConfigs;
using FluentValidation;

namespace Backend.Application.Validators.StockAlertConfigs;

public class CreateStockAlertConfigDtoValidator : AbstractValidator<CreateStockAlertConfigDto>
{
    public CreateStockAlertConfigDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.MinThreshold)
            .GreaterThanOrEqualTo(0)
            .WithMessage("Ngưỡng tồn tối thiểu phải lớn hơn hoặc bằng 0.");

        RuleFor(x => x.ProductVariantId)
            .GreaterThan(0)
            .When(x => x.ProductVariantId.HasValue)
            .WithMessage("SKU (biến thể sản phẩm) không hợp lệ.");
    }
}
