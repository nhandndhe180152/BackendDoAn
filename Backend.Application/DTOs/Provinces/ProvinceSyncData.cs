using System;

namespace Backend.Application.DTOs.Provinces;

public class ProvinceSyncData
{
    public string province_code { get; set; }
    public string name { get; set; }
    public string slug { get; set; }
    public string place_type { get; set; }
    public bool isCentral { get; set; }
    public string fullName { get; set; }
    public List<WardSyncData> wards { get; set; } = new List<WardSyncData>();
}
public class WardSyncData
{
    public string ward_code { get; set; }
    public string name { get; set; }
    public string slug { get; set; }
    public string type { get; set; }
    public string fullName { get; set; }
    public string province_code { get; set; }
}
