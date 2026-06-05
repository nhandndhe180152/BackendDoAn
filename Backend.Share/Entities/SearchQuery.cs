using System;
using System.ComponentModel.DataAnnotations;
using Backend.Share.Attributes;

namespace Backend.Share.Entities;

public class SearchQuery
{
    [Range(1, Int32.MaxValue)]
    public int PageIndex { get; set; } = 1;
    [Range(1, 200)]
    public int PageSize { get; set; } = 10;
    [MaxLength(500)]
    public string Keyword { get; set; } = string.Empty;
    [MaxLength(4), SortTypeValidate]
    public string SortType { get; set; } = "asc";
    public string OrderBy { get; set; } = string.Empty;
}
