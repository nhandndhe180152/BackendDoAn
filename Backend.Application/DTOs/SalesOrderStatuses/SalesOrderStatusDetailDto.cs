using System;

namespace Backend.Application.DTOs.SalesOrderStatuses;

public class SalesOrderStatusDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
