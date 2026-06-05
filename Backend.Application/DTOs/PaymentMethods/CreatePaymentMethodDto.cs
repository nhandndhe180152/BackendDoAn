using System;

namespace Backend.Application.DTOs.PaymentMethods;

public class CreatePaymentMethodDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}
