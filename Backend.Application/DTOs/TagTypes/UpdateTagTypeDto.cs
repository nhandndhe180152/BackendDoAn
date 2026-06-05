using System;

namespace Backend.Application.DTOs.TagTypes;

public class UpdateTagTypeDto : CreateTagTypeDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
