using System;

namespace Backend.Application.DTOs.UnitOfMeasures;

public class UpdateUnitOfMeasureDto : CreateUnitOfMeasureDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
