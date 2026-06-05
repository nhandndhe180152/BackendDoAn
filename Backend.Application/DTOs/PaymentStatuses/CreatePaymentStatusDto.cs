using System;

namespace Backend.Application.DTOs.PaymentStatuses;

public class CreatePaymentStatusDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; } = null!;
    public string Color { get; set; }
    public int CreatedBy { get; set; }
}
