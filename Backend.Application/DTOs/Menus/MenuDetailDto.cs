using System;

namespace Backend.Application.DTOs.Menus;

public class MenuDetailDto
{
    public int Id { get; set; }
    public int? ParentId { get; set; }
    public string TreeIds { get; set; } = null!;
    public string MenuType { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Url { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? ClassName { get; set; }
    public int SortOrder { get; set; } = 1;
    public bool IsAdminOnly { get; set; }
    public DateTime CreatedDate { get; set; }
    public List<int> ActionIds { get; set; } = new List<int>();
}
