using System;
using Backend.Share.Entities;

namespace Backend.Domain.DTParameters;

public class UserDTParameters : DTParameter
{
    public string? Username { get; set; }
    public string? Fullname { get; set; }
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public List<int> UserStatusIds { get; set; } = new List<int>();
    public List<int> RoleIds { get; set; } = new List<int>();

    public int? CurrentUserId { get; set; }
    public bool IsGetAll { get; set; } = true;
}
