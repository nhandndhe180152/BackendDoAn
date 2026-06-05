using System;

namespace Backend.Application.DTOs.Wards;

public class UpdateWardDto : CreateWardDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
