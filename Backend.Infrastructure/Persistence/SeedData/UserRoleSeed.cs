using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class UserRoleSeed
{
    public static List<UserRole> GetUserRoles()
    {
        return new List<UserRole>()
            {
                new UserRole
                {
                    Id=1001,
                    RoleId=1001,
                    UserId=1001,
                }
            };
    }
}
