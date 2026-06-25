using System;

namespace Backend.Application.DTOs.UnitOfMeasures;

public class CreateUnitOfMeasureDto
{
    public string Name { get; set; } = null!;
    public string Symbol { get; set; } = null!;
    public int? CreatedBy { get; set; }
}
