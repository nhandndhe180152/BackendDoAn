using System;

namespace Backend.Application.DTOs.SalesOrderStatuses;

public class UpdateSalesOrderStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? UpdatedBy { get; set; }
}
