using Backend.Application.Constants;
using Backend.Application.DTOs.OutboundOrders;
using FluentValidation;

namespace Backend.Application.Validators.OutboundOrders;

/// <summary>
/// Bắt buộc nhập lý do khi hủy phiếu xuất — chạy tự động qua
/// FluentValidation AutoValidation trước khi vào controller.
/// </summary>
public class CancelOutboundOrderDtoValidator : AbstractValidator<CancelOutboundOrderDto>
{
    public CancelOutboundOrderDtoValidator()
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
