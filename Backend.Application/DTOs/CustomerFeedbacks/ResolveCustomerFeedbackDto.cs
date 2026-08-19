using System;
using System.ComponentModel.DataAnnotations;

namespace Backend.Application.DTOs.CustomerFeedbacks;

public class ResolveCustomerFeedbackDto
{
    [Required]
    [MaxLength(50)]
    public string ResolutionStatus { get; set; } = null!;

    [MaxLength(2000)]
    public string? ResolutionNote { get; set; }
}
