using System;

namespace Backend.Application.DTOs.MillingOrderStatuses;

public class MillingOrderStatusDetailDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
