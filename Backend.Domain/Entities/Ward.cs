using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Ward : EntityAuditBase<int>
{
    //public int DistrictId { get; set; }
    public int ProvinceId { get; set; }
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string ProvinceCode { get; set; } = null!;
    //[JsonIgnore]
    //public virtual District District { get; set; }
    [JsonIgnore]
    public virtual Province Province { get; set; }
    [JsonIgnore]
    public virtual ICollection<User> Users { get; set; }
}
