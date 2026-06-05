using System;

namespace Backend.Application.DTOs.Wards;

public class CreateWardDto
{
    public string Name { get; set; }
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int? ProvinceId { get; set; }
    public string ProvinceCode { get; set; } = null!;
    public int? CreatedBy { get; set; }
}