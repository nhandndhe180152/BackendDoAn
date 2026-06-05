using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Wards;

public class WardDetailDto : DataItem<int>
{
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    //public int DistrictId { get; set; }
    public int? ProvinceId { get; set; }
    public string ProvinceCode { get; set; } = null!;
    public DateTime CreatedDate { get; set; }
}
