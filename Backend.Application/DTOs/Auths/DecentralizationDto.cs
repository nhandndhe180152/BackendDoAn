using System;

namespace Backend.Application.DTOs.Auths;

public class DecentralizationDto
{
    public List<int> UserRoleIds = new List<int>();
    public int? UserId { get; set; }
}
