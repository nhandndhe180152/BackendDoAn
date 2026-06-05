using System;

namespace Backend.Domain.Aggregates;

public class ActivityLogAggregate
{
    public int Id { get; set; }
    public string Action { get; set; } = null!;
    public string? Description { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedDate { get; set; }
    public int? CreatedUserId { get; set; }
    public string? CreatedUserName { get; set; }
}
