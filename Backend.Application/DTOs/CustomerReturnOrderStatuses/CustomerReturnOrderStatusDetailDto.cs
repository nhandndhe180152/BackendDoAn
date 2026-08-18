using System;

namespace Backend.Application.DTOs.CustomerReturnOrderStatuses;

public class CustomerReturnOrderStatusDetailDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
