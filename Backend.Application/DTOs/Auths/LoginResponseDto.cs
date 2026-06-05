using System;
using Backend.Domain.Aggregates;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Auths;

public class LoginResponseDto<T> where T : LoginResponseClientUserInfo
{
    public string AccessToken { get; set; } = null!;
    public string RefreshToken { get; set; } = null!;
    public T UserInfo { get; set; } = default!;
}

public class LoginResponseAdminUserInfo : LoginResponseClientUserInfo
{
    public List<DataItem<int>> Roles { get; set; } = new List<DataItem<int>>();
    public List<PermissionAggregate> Permissions { get; set; } = new List<PermissionAggregate>();
    public List<MenuAggregate> Menus { get; set; } = new List<MenuAggregate>();

}

public class LoginResponseClientUserInfo
{
    public int Id { get; set; }
    public string FullName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
    public string Email { get; set; } = null!;
}

public class LoginResponsePermission()
{
    public int MenuId { get; set; }
    public List<int> ActionIds { get; set; } = new List<int>();
}
