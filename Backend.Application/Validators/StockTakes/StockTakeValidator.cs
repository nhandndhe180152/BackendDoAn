using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakes;
using FluentValidation;

namespace Backend.Application.Validators.StockTakes;

public class CreateStockTakeDtoValidator : AbstractValidator<CreateStockTakeDto>
{
    public CreateStockTakeDtoValidator()
    {
        RuleFor(x => x.WarehouseId)
            .GreaterThan(0)
            .WithName("Kho")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.StockTakeStatusId)
            .GreaterThan(0)
            .WithName("Trạng thái")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
            
        RuleForEach(x => x.StockTakeItems).SetValidator(new CreateStockTakeItemDtoValidator());
    }
}

public class UpdateStockTakeDtoValidator : AbstractValidator<UpdateStockTakeDto>
{
    public UpdateStockTakeDtoValidator()
    {
        RuleFor(x => x.Id)
            .GreaterThan(0)
            .WithName("Id")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.StockTakeStatusId)
            .GreaterThan(0)
            .WithName("Trạng thái")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage));

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
            
        RuleForEach(x => x.StockTakeItems).SetValidator(new UpdateStockTakeItemDtoValidator());
    }
}

public class CreateStockTakeItemDtoValidator : AbstractValidator<CreateStockTakeItemDto>
{
    public CreateStockTakeItemDtoValidator()
    {
        RuleFor(x => x.SystemQuantity)
            .GreaterThanOrEqualTo(0)
            .WithName("Số lượng hệ thống")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData));

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}

public class UpdateStockTakeItemDtoValidator : AbstractValidator<UpdateStockTakeItemDto>
{
    public UpdateStockTakeItemDtoValidator()
    {
        RuleFor(x => x.SystemQuantity)
            .GreaterThanOrEqualTo(0)
            .WithName("Số lượng hệ thống")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData));

        RuleFor(x => x.Note)
            .MaximumLength(500)
            .WithName("Ghi chú")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
