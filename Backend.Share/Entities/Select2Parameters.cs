using System;

namespace Backend.Share.Entities;

public class Select2Parameters
{
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
}
