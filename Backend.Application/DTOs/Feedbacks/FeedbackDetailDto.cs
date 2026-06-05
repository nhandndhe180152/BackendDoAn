using System;

namespace Backend.Application.DTOs.Feedbacks;

public class FeedbackDetailDto
{
    public int Id { get; set; }
    public string Title { get; set; } = null!;
    public string Content { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
