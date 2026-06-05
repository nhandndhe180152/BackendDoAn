using System;

namespace Backend.Share.Entities;

public class DataItem<TKey>
{
    public TKey Id { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
}