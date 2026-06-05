using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public class RoleSeed
{
    public static List<Role> GetRoles()
    {
        return new List<Role>
            {
                new Role
                {
                    Id=1001,
                    Name="Quản trị viên",
                },
                new Role
                {
                    Id=1002,
                    Name="Người dùng",
                }
            };
    }
}
