using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

public class SystemConfig : EntityCommonBase<int>
{
    public string ConfigKey { get; set; } = null!;
    public string ConfigValue { get; set; } = null!;
}
