using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.SalesOrders;

public class CreateOutboundDto
{
    [Required]
    public List<CreateOutboundItemDto> Items { get; set; } = new();
}

public class CreateOutboundItemDto
{
    [Required]
    public int ProductVariantId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Số lượng xuất phải lớn hơn 0")]
    public decimal QuantityToDispatch { get; set; }
}
