using System;

namespace Backend.Application.DTOs.Actions;

public class UpdateActionDto : CreateActionDto
{
    public int Id { get; set; }
    public int UpdatedBy { get; set; }
}
