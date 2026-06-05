using System;

namespace Backend.Application.DTOs.Menus;

public class UpdateMenuDto : CreateMenuDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
}
