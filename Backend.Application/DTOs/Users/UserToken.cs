using System;

namespace Backend.Application.DTOs.Users;

public class UserToken
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string AccessTokenJti { get; set; } = Guid.NewGuid().ToString();
    public int? OfficeId { get; set; }
    public int? DriverId { get; set; }
    public List<int> RoleIds { get; set; } = new List<int>();
    public string FullName { get; set; } = string.Empty;
}
