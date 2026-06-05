# Backend.Domain — Project Rules

> Domain Layer — Trái tim của Clean Architecture. Không phụ thuộc vào bất kỳ project nào khác trong solution.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Định nghĩa **Entity** (ánh xạ với DB table)
- Định nghĩa **Repository interface** (contract cho Infrastructure implement)
- Định nghĩa **Aggregate** (custom projection/join result, không map DB)
- Định nghĩa **Enum** và **DTParameters** cho paging nâng cao
- Chứa **domain rules thuần túy** — không có EF, không có HTTP, không có DI

---

## Cấu trúc thư mục

```
Backend.Domain/
├── Abstractions/           # Base classes cho Entity
│   ├── EntityBase.cs           # Id + IsDeleted
│   ├── EntityAuditBase.cs      # + CreatedDate, UpdatedDate, CreatedBy, UpdatedBy
│   ├── EntityCommonBase.cs     # + Name, Description
│   └── EntityFullTextSearch.cs # + SearchVector (nếu dùng full-text)
├── Entities/               # DB entities (1 class = 1 table)
│   ├── User.cs
│   ├── Role.cs
│   └── ...
├── Aggregates/             # Custom projection — kết quả join/query phức tạp
│   ├── UserAggregates.cs       # Projection user + roles + status
│   └── ...
├── Interfaces/
│   └── Repositories/       # Repository contracts
│       ├── IRepositoryBase.cs
│       ├── IUserRepository.cs
│       └── ...
├── DTParameters/           # Paging params nâng cao cho DataTable
│   ├── UserDTParameters.cs
│   └── ...
├── Enums/
│   └── Enums.cs            # Tất cả enum của hệ thống
├── Commons/
│   └── DTResultWithSummary.cs
└── Backend.Domain.csproj
```

---

## Quy tắc Entity

### Chọn base class phù hợp

```
EntityBase<TKey>           → chỉ cần Id + IsDeleted
EntityAuditBase<TKey>      → + CreatedDate, UpdatedDate, CreatedBy, UpdatedBy
EntityCommonBase<TKey>     → + Name, Description (CRUD cơ bản nhất)
```

### Khai báo Entity

```csharp
// Domain/Entities/SomeEntity.cs
public class SomeEntity : EntityCommonBase<int>
{
    // Chỉ khai báo property thuần
    // Navigation properties cho relationship
    public int UserId { get; set; }
    public virtual User User { get; set; } = null!;  // dùng virtual cho lazy loading nếu cần

    // Collection navigation
    public virtual ICollection<ChildEntity> Children { get; set; } = new List<ChildEntity>();
}
```

**Quy tắc Entity:**
- Primary key thường là `int` — dùng generic `TKey` nếu cần UUID
- Tên class **số ít**, PascalCase
- Navigation properties dùng `virtual` (EF convention)
- **Không** chứa business method hay logic trong Entity
- **Không** dùng `[Column]`, `[Table]` annotation — tất cả config bằng **Fluent API** ở Infrastructure

---

## Quy tắc Repository Interface

```csharp
// Domain/Interfaces/Repositories/IUserRepository.cs
public interface IUserRepository : IRepositoryBase<User, int>
{
    // Chỉ khai báo query đặc thù KHÔNG có trong IRepositoryBase
    Task<UserAggregates?> GetUserWithRolesAsync(int userId);
    Task<List<User>> GetByRoleAsync(int roleId);
}
```

**Các method đã có sẵn trong `IRepositoryBase` (không cần khai báo lại):**
- `FindByCondition(expression)` → IQueryable
- `FindByConditionAsync(expression)` → List
- `FirstOrDefaultAsync(expression)` → entity hoặc null
- `AnyAsync(expression)` → bool
- `CountByConditionAsync(expression)` → int
- `CreateAsync(entity)`
- `CreateListAsync(entities)`
- `UpdateAsync(entity)`
- `UpdateListAsync(entities)`
- `DeleteAsync(entity)` — xóa thật (dùng ít, prefer SoftDelete)
- `SaveChangesAsync()`
- `BeginTransactionAsync()` / `EndTransactionAsync()` / `RollbackTransactionAsync()`

---

## Quy tắc Aggregate

Aggregate là **read-only projection** — không map DB, chỉ dùng để trả về kết quả query phức tạp.

```csharp
// Domain/Aggregates/UserAggregates.cs
public class UserAggregates
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string UserStatusName { get; set; } = null!;
    public string UserStatusColor { get; set; } = null!;
    public List<Role> Roles { get; set; } = new List<Role>();
    // ...
}
```

**Khi nào dùng Aggregate vs Entity:**
- Entity → CRUD đơn giản trên 1 bảng
- Aggregate → Join nhiều bảng, custom projection, kết quả query phức tạp

---

## Quy tắc DTParameters (paging DataTable)

```csharp
// Domain/DTParameters/UserDTParameters.cs
public class UserDTParameters : DTParameter  // DTParameter từ Share
{
    // Filter fields đặc thù cho entity này
    public int? UserStatusId { get; set; }
    public int? RoleId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
}
```

---

## Enums (Domain/Enums/Enums.cs)

Tất cả enum của hệ thống đặt trong `Enums.cs` dưới static class `Enums`:

```csharp
public static class Enums
{
    public enum UserStatus { NotActivated = 1001, Actived = 1002, Locked = 1003, Deactivated = 1004 }
    public enum Action { CREATE = 1001, READ, UPDATE, DELETE, EXPORT, APPROVE }
    public enum Gender { FEMALE = 0, MALE = 1, OTHER = 2 }
    public enum Role { ADMIN = 1001, USER }
    // ...
}
```

**Quy tắc Enum:**
- Enum value bắt đầu từ `1001` (tránh nhầm với `0`, `false`)
- Tên enum value dùng `UPPER_SNAKE_CASE`
- Thêm XML comment nếu tên không tự giải thích

---

## ⚠️ Domain KHÔNG được chứa

- Reference đến EF Core, Dapper, hay bất kỳ ORM nào
- Reference đến `Microsoft.AspNetCore.*`
- Business logic phức tạp (đặt ở Application)
- Annotation như `[Column]`, `[Table]`, `[Key]` — dùng Fluent API ở Infrastructure
