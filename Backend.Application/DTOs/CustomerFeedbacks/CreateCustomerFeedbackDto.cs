using System;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.CustomerFeedbacks;

public class CreateCustomerFeedbackDto
{
    [Required]
    public int SalesOrderId { get; set; }

    [Required]
    public int OutboundOrderId { get; set; }

    public int? OutboundOrderItemId { get; set; }
    public int? ProductVariantId { get; set; }
    public int? PaddyLotBagAllocationId { get; set; }

    [Required]
    [MaxLength(50)]
    public string FeedbackType { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = null!;

    [MaxLength(50)]
    public string? Severity { get; set; }
}
