using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using DomainAction = Backend.Domain.Entities.Action;

namespace Backend.Infrastructure.Services;

/// <summary>
/// Phân giải Code → Id cho các bảng lookup, nạp từ DB vào dictionary trong bộ nhớ (cache).
/// Singleton: dùng IServiceScopeFactory để mở scope lấy DbContext khi nạp lại.
/// </summary>
public class SystemLookup : ISystemLookup
{
    private readonly IServiceScopeFactory _scopeFactory;

    private Dictionary<string, int> _menu = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _action = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _role = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _userStatus = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _stockTakeStatus = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, int> _paddyScheduleStatus = new(StringComparer.OrdinalIgnoreCase);

    private volatile bool _loaded;
    private readonly object _gate = new();

    public SystemLookup(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task ReloadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BackendContext>();

        _menu = await LoadAsync<Menu>(db);
        _action = await LoadAsync<DomainAction>(db);
        _role = await LoadAsync<Role>(db);
        _userStatus = await LoadAsync<UserStatus>(db);
        _stockTakeStatus = await LoadAsync<StockTakeStatus>(db);
        _paddyScheduleStatus = await LoadAsync<PaddyPurchaseScheduleStatus>(db);

        _loaded = true;
    }

    public int MenuId(string code) => Resolve(_menu, code, nameof(Menu));
    public int ActionId(string code) => Resolve(_action, code, "Action");
    public int RoleId(string code) => Resolve(_role, code, nameof(Role));

    public int RoleIdOrDefault(string code, int fallback)
    {
        EnsureLoaded();
        return !string.IsNullOrEmpty(code) && _role.TryGetValue(code, out var id) ? id : fallback;
    }
    public int UserStatusId(string code) => Resolve(_userStatus, code, nameof(UserStatus));
    public int StockTakeStatusId(string code) => Resolve(_stockTakeStatus, code, nameof(StockTakeStatus));
    public int PaddyScheduleStatusId(string code) => Resolve(_paddyScheduleStatus, code, nameof(PaddyPurchaseScheduleStatus));

    public bool TryMenuId(string code, out int id) => TryResolve(_menu, code, out id);
    public bool TryActionId(string code, out int id) => TryResolve(_action, code, out id);

    private int Resolve(Dictionary<string, int> dict, string code, string kind)
    {
        EnsureLoaded();
        if (!string.IsNullOrEmpty(code) && dict.TryGetValue(code, out var id))
            return id;

        throw new InvalidOperationException(
            $"Không tìm thấy {kind} có Code = '{code}'. Hãy kiểm tra seed/backfill cột Code của bảng {kind}.");
    }

    private bool TryResolve(Dictionary<string, int> dict, string code, out int id)
    {
        EnsureLoaded();
        id = 0;
        return !string.IsNullOrEmpty(code) && dict.TryGetValue(code, out id);
    }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        lock (_gate)
        {
            if (!_loaded)
                ReloadAsync().GetAwaiter().GetResult();
        }
    }

    private static async Task<Dictionary<string, int>> LoadAsync<TEntity>(BackendContext db)
        where TEntity : class
    {
        // Chỉ lấy các dòng có Code (không xóa mềm); dòng trùng Code thì dòng sau ghi đè (an toàn).
        var pairs = await db.Set<TEntity>()
            .AsNoTracking()
            .Select(x => new CodeId
            {
                Code = EF.Property<string?>(x, "Code"),
                Id = EF.Property<int>(x, "Id"),
                IsDeleted = EF.Property<bool>(x, "IsDeleted")
            })
            .Where(x => x.Code != null && !x.IsDeleted)
            .ToListAsync();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in pairs)
            dict[p.Code!] = p.Id;
        return dict;
    }

    private sealed class CodeId
    {
        public string? Code { get; set; }
        public int Id { get; set; }
        public bool IsDeleted { get; set; }
    }
}
