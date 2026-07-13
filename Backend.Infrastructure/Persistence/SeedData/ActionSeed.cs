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
                    Code="CREATE",
                    Name="Thêm mới",
                },
                new Domain.Entities.Action{
                    Id=1002,
                    Code="READ",
                    Name="Xem",
                },
                new Domain.Entities.Action{
                    Id=1003,
                    Code="UPDATE",
                    Name="Chỉnh sửa",
                },
                new Domain.Entities.Action{
                    Id=1004,
                    Code="DELETE",
                    Name="Xoá",
                },
                new Domain.Entities.Action{
                    Id=1005,
                    Code="EXPORT",
                    Name="Xuất dữ liệu",
                },
                new Domain.Entities.Action{
                    Id=1006,
                    Code="APPROVE",
                    Name="Duyệt",
                }
            };
    }
}
