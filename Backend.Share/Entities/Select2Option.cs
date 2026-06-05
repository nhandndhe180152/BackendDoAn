using System;

namespace Backend.Share.Entities;

public class Select2Option<T>
{
    public T Id { get; set; }
    public string Text { get; set; } = null!;
}
