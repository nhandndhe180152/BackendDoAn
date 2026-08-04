using System;

namespace Backend.Application.DTOs.ReturnToSupplierOrderStatuses;

public class CreateReturnToSupplierOrderStatusDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
