using System;

namespace Backend.Application.DTOs.ReturnToSupplierOrderStatuses;

public class ReturnToSupplierOrderStatusDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
