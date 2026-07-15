namespace Backend.Application.Interfaces;

/// <summary>
/// Bộ phân giải Code → Id (số) cho các bảng lookup hệ thống, nạp từ DB và cache trong bộ nhớ.
/// Cho phép code tham chiếu tới dữ liệu bằng Code ổn định thay vì Id số hard-code:
/// Id số trong DB đổi/re-seed vẫn không vỡ, vì Id được suy ra từ Code lúc chạy.
/// </summary>
public interface ISystemLookup
{
    int MenuId(string code);
    int ActionId(string code);
    int RoleId(string code);

    /// <summary>Phân giải RoleId theo Code; nếu chưa nạp/không thấy thì trả về fallback (không ném lỗi).
    /// Dùng cho các role có thể chưa được seed (Driver/Dispatcher/Executive).</summary>
    int RoleIdOrDefault(string code, int fallback);

    int UserStatusId(string code);
    int StockTakeStatusId(string code);
    int PaddyScheduleStatusId(string code);

    /// <summary>Thử phân giải, trả về false thay vì ném lỗi khi không thấy Code.</summary>
    bool TryMenuId(string code, out int id);
    bool TryActionId(string code, out int id);

    /// <summary>Nạp lại toàn bộ dictionary từ DB (gọi lúc khởi động và sau khi CRUD bảng lookup).</summary>
    Task ReloadAsync();
}
