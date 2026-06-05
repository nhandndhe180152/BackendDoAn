using System;

namespace Backend.Share.Entities;

public class AdvancedSearchQuery<T> : SearchQuery
{
    public T? SearchOptions { get; set; }
}
