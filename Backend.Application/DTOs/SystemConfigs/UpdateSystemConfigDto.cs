using System;

namespace Backend.Application.DTOs.SystemConfigs;

public class UpdateSystemConfigDto : CreateSystemConfigDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
