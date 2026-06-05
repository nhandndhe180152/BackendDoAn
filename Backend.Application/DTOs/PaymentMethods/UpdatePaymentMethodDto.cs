using System;

namespace Backend.Application.DTOs.PaymentMethods;

public class UpdatePaymentMethodDto : CreatePaymentMethodDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
