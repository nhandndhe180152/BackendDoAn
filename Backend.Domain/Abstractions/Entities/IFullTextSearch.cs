using System;

namespace Backend.Domain.Abstractions.Entities;

public interface IFullTextSearch
{
    public string Search { get; set; }
}
