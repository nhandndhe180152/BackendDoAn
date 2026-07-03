using System;

namespace Backend.Application.DTOs.Users;

/// <summary>
/// Số liệu tổng hợp người dùng (tính trên toàn bộ user, không theo trang).
/// </summary>
public class UserStatisticsDto
{
    public int TotalUsers { get; set; }
    public int ActiveUsers { get; set; }
    public List<RoleCountDto> RoleCounts { get; set; } = new List<RoleCountDto>();
}

public class RoleCountDto
{
    public int RoleId { get; set; }
    public string RoleName { get; set; } = null!;
    public int Count { get; set; }
}
