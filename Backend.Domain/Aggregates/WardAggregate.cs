using System;

namespace Backend.Domain.Aggregates;

public class WardAggregate
{
    public int Id { get; set; }
    public int ProvinceId { get; set; }
    public string ProvinceName { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string TypeName { get; set; } = null!;
    public string ProvinceCode { get; set; } = null!;
}
