using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Backend.Application.Common;
using Backend.Application.Interfaces;

namespace Backend.UnitTest.Common;

/// <summary>
/// Unit test không chạy startup/SystemCacheWarmup nên facade tĩnh <see cref="Lookup"/> chưa được gắn.
/// Module initializer này gắn 1 stub <see cref="ISystemLookup"/> trả về ĐÚNG Id seed cũ (theo Code)
/// trước khi bất kỳ test nào chạy — giữ nguyên kỳ vọng của test, không phải sửa từng test.
/// </summary>
internal static class TestLookupInitializer
{
    [ModuleInitializer]
    internal static void Init()
    {
        Lookup.Configure(new TestSystemLookup());
    }
}

internal sealed class TestSystemLookup : ISystemLookup
{
    public int MenuId(string code) => 0;

    public int ActionId(string code) => code switch
    {
        "CREATE" => 1001,
        "READ" => 1002,
        "UPDATE" => 1003,
        "DELETE" => 1004,
        "EXPORT" => 1005,
        "APPROVE" => 1006,
        _ => 0
    };

    public int RoleId(string code) => code switch
    {
        "ADMIN" => 1001,
        "OWNER" => 1002,
        "PURCHASING" => 1003,
        "WAREHOUSE" => 1004,
        "MILLING" => 1005,
        "SALES" => 1006,
        _ => 0
    };

    public int RoleIdOrDefault(string code, int fallback)
    {
        var id = RoleId(code);
        return id != 0 ? id : fallback;
    }

    public int UserStatusId(string code) => code switch
    {
        "NotActivated" => 1001,
        "Actived" => 1002,
        "Locked" => 1003,
        "Deactivated" => 1004,
        _ => 0
    };

    public int StockTakeStatusId(string code) => code switch
    {
        "Draft" => 1,
        "Submitted" => 2,
        "Approved" => 3,
        "Rejected" => 4,
        _ => 0
    };

    public int PaddyScheduleStatusId(string code) => code switch
    {
        "NEW" => 1,
        "CONFIRMED" => 2,
        "COLLECTING" => 3,
        "WEIGHED" => 4,
        "STOCKED" => 5,
        "CANCELLED" => 6,
        _ => 0
    };

    public bool TryMenuId(string code, out int id) { id = MenuId(code); return id != 0; }

    public bool TryActionId(string code, out int id) { id = ActionId(code); return id != 0; }

    public Task ReloadAsync() => Task.CompletedTask;
}
