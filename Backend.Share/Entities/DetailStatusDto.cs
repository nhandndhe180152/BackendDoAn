using System;

namespace Backend.Share.Entities;

public class DetailStatusDto<T> : DataItem<T>
{
    public string Color { get; set; } = null!;
}
