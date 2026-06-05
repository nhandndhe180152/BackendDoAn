using System;
using Backend.Share.Entities;

namespace Backend.Application.DTOs.Users;

public class UserSearchQuery : SearchQuery
{
    public List<int> RoleIds { get; set; } = new List<int>();
}
