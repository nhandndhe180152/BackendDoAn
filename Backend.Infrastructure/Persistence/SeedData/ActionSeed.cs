using System;

namespace Backend.Infrastructure.Persistence.SeedData;

public static class ActionSeed
{
    public static List<Domain.Entities.Action> GetActions()
    {
        return new List<Domain.Entities.Action>()
            {
                new Domain.Entities.Action{
                    Id=1001,
                    Name="Thêm mới",
                },
                new Domain.Entities.Action{
                    Id=1002,
                    Name="Xem",
                },
                new Domain.Entities.Action{
                    Id=1003,
                    Name="Chỉnh sửa",
                },
                new Domain.Entities.Action{
                    Id=1004,
                    Name="Xoá",
                },
                new Domain.Entities.Action{
                    Id=1005,
                    Name="Xuất dữ liệu",
                },
                new Domain.Entities.Action{
                    Id=1006,
                    Name="Duyệt",
                }
            };
    }
}
