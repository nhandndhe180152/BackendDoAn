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
                    Code="NotActivated",
                    Name="Chưa kích hoạt",
                    Color="#ff9500",
                },
                new UserStatus
                {
                    Id=1002,
                    Code="Actived",
                    Name="Đang hoạt động",
                    Color="#00b315",
                },
                new UserStatus
                {
                    Id=1003,
                    Code="Locked",
                    Name="Bị khoá",
                    Color="#ff0000",
                },
                new UserStatus
                {
                    Id=1004,
                    Code="Deactivated",
                    Name="Ngưng hoạt động",
                    Color="#787878"
                }
            };
    }
}
