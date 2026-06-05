using System;

namespace Backend.Domain.Aggregates;

public class TagAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int TagTypeId { get; set; }
    public string? TagTypeName { get; set; }
    public DateTime CreatedDate { get; set; }
}
