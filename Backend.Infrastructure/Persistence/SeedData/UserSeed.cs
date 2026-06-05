using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class UserSeed
    {
        public static List<User> GetUsers()
        {
            return new List<User>
            {
                new User
                {
                    Id=1001,
                    FirstName="System",
                    LastName="Admin",
                    Email="systemadmin@gmail.vn",
                    Gender=1,
                    Username="admin",
                    PasswordHash="$2a$11$kH4XY8m7bRFmUHhvuMlznOhLH74exbW2sjXnO0TSOkDQK4q/0gfVG",
                    UserStatusId=1002
                }
            };
        }
    }
