using System.Collections.Generic;

namespace Backend.Application.DTOs.MillingOrders;

public class ReserveMillingOrderDto
{
    public List<MillingBagSelectionColumnDto> Columns { get; set; } = new();
    [System.Obsolete("Use Columns with physical BagIds.")]
    public List<MillingOrderInputItemDto> Inputs { get; set; } = new();
    public int? UpdatedBy { get; set; }
}

public class MillingBagSelectionColumnDto
{
    public int LocationId { get; set; }
    public List<int> BagIds { get; set; } = new();
}
