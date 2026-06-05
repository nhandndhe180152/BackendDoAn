using System;

namespace Backend.Application.DTOs.UserStatuses;

public class UserStatusDetailDto
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Description { get; set; } = "";
    public string Color { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
