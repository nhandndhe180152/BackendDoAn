using System;

namespace Backend.Application.DTOs.SalesOrderStatuses;

public class CreateSalesOrderStatusDto
{
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
