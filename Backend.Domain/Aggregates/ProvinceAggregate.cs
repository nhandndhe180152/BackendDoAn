using System;

namespace Backend.Domain.Aggregates;

public class ProvinceAggregate
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string TypeName { get; set; } = null!;
    public bool IsCentral { get; set; }
}
