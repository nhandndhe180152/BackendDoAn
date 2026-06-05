using System;

namespace Backend.Domain.Abstractions.Entities;

public interface IEntityCommonBase
{
    string Name { get; set; }
    string? Description { get; set; }
}
