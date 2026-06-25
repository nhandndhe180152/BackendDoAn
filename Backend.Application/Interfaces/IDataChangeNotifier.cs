using System.Collections.Generic;
using System.Threading.Tasks;

namespace Backend.Application.Interfaces;

/// <summary>
/// Phát tín hiệu "dữ liệu của các entity này vừa thay đổi" tới client (SignalR).
/// Chỉ gửi danh sách tên entity (vài byte), KHÔNG gửi dữ liệu. FE nhận tín hiệu
/// rồi tự gọi lại API cần thiết (invalidate query).
/// </summary>
public interface IDataChangeNotifier
{
    Task NotifyEntitiesChangedAsync(IReadOnlyCollection<string> entityNames);
}
