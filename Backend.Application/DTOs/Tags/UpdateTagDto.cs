using System;

namespace Backend.Application.DTOs.Tags;

public class UpdateTagDto : CreateTagDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
