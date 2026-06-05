using System;

namespace Backend.Share.Entities;

public class DistrictApiResponse
{
    public string name { get; set; } = null!;
    public int code { get; set; }
    public string division_type { get; set; } = null!;
    public string? codename { get; set; }
    public int province_code { get; set; }
}
