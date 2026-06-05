using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Provinces;

public class ProvinceDetailDto : DataItem<int>
{
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsCentral { get; set; }
    public DateTime CreatedDate { get; set; }
}
