using System;

namespace Backend.Domain.Aggregates;

public class PermissionAggregate
{
    public int MenuId { get; set; }
    public List<int> ActionIds { get; set; } = new List<int>();
}
