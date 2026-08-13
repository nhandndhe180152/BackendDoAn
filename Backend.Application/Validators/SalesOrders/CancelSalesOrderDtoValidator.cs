using Backend.Application.Constants;
using Backend.Application.DTOs.SalesOrders;
using FluentValidation;

namespace Backend.Application.Validators.SalesOrders;

/// <summary>
/// Bắt buộc nhập lý do khi hủy đơn bán — chạy tự động qua
/// FluentValidation AutoValidation trước khi vào controller.
/// </summary>
public class CancelSalesOrderDtoValidator : AbstractValidator<CancelSalesOrderDto>
{
    public CancelSalesOrderDtoValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty()
            .WithName("Lý do hủy")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage))
            .MaximumLength(500)
            .WithName("Lý do hủy")
            .WithMessage(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.MaxLengthMessage));
    }
}
