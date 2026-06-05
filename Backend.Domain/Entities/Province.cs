using System;
using System.Text.Json.Serialization;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class Province : EntityAuditBase<int>
{
    public string Name { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsCentral { get; set; }
    //[JsonIgnore]
    //public virtual ICollection<District> Districts { get; set; }
    [JsonIgnore]
    public virtual ICollection<Ward> Wards { get; set; }
    [JsonIgnore]
    public virtual ICollection<User> Users { get; set; }
}
