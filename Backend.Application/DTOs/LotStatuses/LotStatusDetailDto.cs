using System;

namespace Backend.Application.DTOs.LotStatuses;

public class LotStatusDetailDto
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public bool IsSellable { get; set; }
    public DateTime CreatedDate { get; set; }
}
