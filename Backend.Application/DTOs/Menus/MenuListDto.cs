using System;

namespace Backend.Application.DTOs.Menus;

public class MenuListDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string TreeIds { get; set; }
    public string MenuType { get; set; } = "ADMIN";
    public string Name { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ClassName { get; set; }
    public int SortOrder { get; set; } = 1;
    public List<MenuListDto>? Child { get; set; }
}
