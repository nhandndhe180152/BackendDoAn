using System;

namespace Backend.Domain.Aggregates;

public class RoleAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTime CreatedDate { get; set; }
}
