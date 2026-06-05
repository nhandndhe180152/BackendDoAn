using System;

namespace Backend.Application.DTOs.Feedbacks;

public class UpdateFeedbackDto : CreateFeedbackDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
