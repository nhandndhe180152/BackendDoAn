using Backend.Application.Interfaces;

namespace Backend.Application.Common;

/// <summary>
/// Facade tĩnh cho <see cref="ISystemLookup"/> để dùng ở cả service lẫn static mapper mà không phải
/// inject qua constructor. Được gắn 1 lần lúc khởi động qua <see cref="Configure"/>.
/// </summary>
public static class Lookup
{
    private static ISystemLookup? _impl;

    public static void Configure(ISystemLookup impl) => _impl = impl;

    private static ISystemLookup Impl =>
        _impl ?? throw new InvalidOperationException(
            "SystemLookup chưa được khởi tạo. Đảm bảo Lookup.Configure(...) được gọi lúc startup (SystemCacheWarmup).");

    public static int MenuId(string code) => Impl.MenuId(code);
    public static int ActionId(string code) => Impl.ActionId(code);
    public static int RoleId(string code) => Impl.RoleId(code);
    public static int UserStatusId(string code) => Impl.UserStatusId(code);
    public static int StockTakeStatusId(string code) => Impl.StockTakeStatusId(code);

    /// <summary>
    /// An toàn cho field/static initializer: nếu facade chưa gắn hoặc Code chưa seed thì trả fallback
    /// (KHÔNG ném lỗi). Cho phép <c>CommonConstants.Role.*</c> chạy được kể cả trước khi warmup.
    /// </summary>
    public static int RoleIdOrDefault(string code, int fallback)
    {
        var impl = _impl;
        if (impl == null) return fallback;
        try { return impl.RoleIdOrDefault(code, fallback); }
        catch { return fallback; }
    }
}
