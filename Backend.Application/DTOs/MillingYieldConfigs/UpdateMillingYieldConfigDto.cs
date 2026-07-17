using System;

namespace Backend.Application.DTOs.MillingYieldConfigs;

public class UpdateMillingYieldConfigDto : CreateMillingYieldConfigDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
