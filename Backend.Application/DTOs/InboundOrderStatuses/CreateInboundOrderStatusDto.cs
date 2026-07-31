using System;

namespace Backend.Application.DTOs.InboundOrderStatuses;

public class CreateInboundOrderStatusDto
{
    public string Name { get; set; } = null!;
    public string Color { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
