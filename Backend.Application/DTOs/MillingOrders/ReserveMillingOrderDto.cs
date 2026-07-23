using System.Collections.Generic;

namespace Backend.Application.DTOs.MillingOrders;

public class ReserveMillingOrderDto
{
    public List<MillingOrderInputItemDto> Inputs { get; set; } = new();
    public int? UpdatedBy { get; set; }
}
