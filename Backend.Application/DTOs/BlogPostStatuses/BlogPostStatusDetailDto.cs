using System;

namespace Backend.Application.DTOs.BlogPostStatuses;

public class BlogPostStatusDetailDto
{
    public int Id { get; set; } = 0;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
