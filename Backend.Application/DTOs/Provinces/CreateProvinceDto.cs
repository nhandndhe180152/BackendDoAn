using System;

namespace Backend.Application.DTOs.Provinces;

public class CreateProvinceDto
{
    public string Name { get; set; }
    public string Code { get; set; } = null!;
    public string Slug { get; set; } = null!;
    public string Type { get; set; } = null!;
    public bool IsCentral { get; set; }
    public int? CreatedBy { get; set; }
}
