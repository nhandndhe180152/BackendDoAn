using System;

namespace Backend.Application.DTOs.Actions;

public class CreateActionDto
{
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public int? CreatedBy { get; set; }
}