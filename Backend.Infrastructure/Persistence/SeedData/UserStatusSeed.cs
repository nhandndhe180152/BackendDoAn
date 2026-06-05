using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class UserStatusSeed
{
    public static List<UserStatus> GetUserStatuses()
    {
        return new List<UserStatus>
            {
                new UserStatus
                {
                    Id=1001,
                    Name="Chưa kích hoạt",
                    Color="#ff9500",
                },
                new UserStatus
                {
                    Id=1002,
                    Name="Đang hoạt động",
                    Color="#00b315",
                },
                new UserStatus
                {
                    Id=1003,
                    Name="Bị khoá",
                    Color="#ff0000",
                },
                new UserStatus
                {
                    Id=1004,
                    Name="Ngưng hoạt động",
                    Color="#787878"
                }
            };
    }
}
