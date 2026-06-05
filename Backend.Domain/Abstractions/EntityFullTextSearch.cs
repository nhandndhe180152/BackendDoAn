using System;
using Backend.Domain.Abstractions.Entities;

namespace Backend.Domain.Abstractions;

public abstract class EntityFullTextSearch : IFullTextSearch
{
    public string? Search { get; set; }
}
