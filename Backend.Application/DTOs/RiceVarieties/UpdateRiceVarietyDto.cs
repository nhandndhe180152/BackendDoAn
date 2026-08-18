using System;

namespace Backend.Application.DTOs.RiceVarieties;

public class UpdateRiceVarietyDto : CreateRiceVarietyDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
