using System;

namespace Backend.Application.DTOs.Feedbacks;

public class CreateFeedbackDto
{
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
