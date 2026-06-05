using System;

namespace Backend.Application.DTOs.Provinces;

public class UpdateProvinceDto : CreateProvinceDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
