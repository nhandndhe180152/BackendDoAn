using System;

namespace Backend.Application.DTOs.Users;

public class UpdateUserDto
{
    public int Id { get; set; }
    public int? UpdatedBy { get; set; }
    public int UserStatusId { get; set; }
    public bool LockEnabled { get; set; }
    public DateTime? LockEndDate { get; set; }
    public List<int> Roles { get; set; } = new List<int>();

}
