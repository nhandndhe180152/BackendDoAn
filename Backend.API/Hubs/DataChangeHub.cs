using Microsoft.AspNetCore.SignalR;

namespace Backend.API.Hubs;

/// <summary>
/// Hub realtime dùng chung. Server chỉ phát sự kiện "EntityChanged" kèm danh sách
/// tên entity vừa thay đổi để client tự làm mới dữ liệu (không đẩy payload).
/// </summary>
public class DataChangeHub : Hub
{
    // Client lắng nghe sự kiện "EntityChanged" (string[] entityNames).
}
