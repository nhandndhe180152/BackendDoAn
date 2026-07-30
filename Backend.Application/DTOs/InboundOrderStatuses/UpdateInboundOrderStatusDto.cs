using System;

namespace Backend.Application.DTOs.InboundOrderStatuses;

public class UpdateInboundOrderStatusDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? UpdatedBy { get; set; }
}
