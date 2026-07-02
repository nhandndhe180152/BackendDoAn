using System;

namespace Backend.Application.DTOs.Suppliers;

public class UpdateSupplierDto : CreateSupplierDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
