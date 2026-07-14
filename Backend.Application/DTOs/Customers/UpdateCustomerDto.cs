using System;

namespace Backend.Application.DTOs.Customers;

public class UpdateCustomerDto : CreateCustomerDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
