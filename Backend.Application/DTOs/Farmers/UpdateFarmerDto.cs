using System;

namespace Backend.Application.DTOs.Farmers;

public class UpdateFarmerDto : CreateFarmerDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
