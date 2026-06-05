# Backend.Infrastructure — Project Rules

> Infrastructure Layer — Implement các interface từ Domain. Chứa tất cả external concerns: DB, email, file storage, jobs.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Implement `IXxxRepository` interface được định nghĩa ở `Domain/Interfaces/`
- Cấu hình **Entity Framework Core** (DbContext, Configurations, Migrations)
- Implement các **external services**: Cloudinary (storage), Firebase (push notification), Gmail (email)
- Chạy **background jobs** với Hangfire (cleanup session, cleanup tokens)
- Chứa **interceptors** EF Core (audit log tự động)

---

## Cấu trúc thư mục

```
Backend.Infrastructure/
├── Persistence/
│   ├── BackendContext.cs           # DbContext chính
│   ├── Configurations/             # Fluent API config cho từng Entity
│   │   ├── UserConfiguration.cs
│   │   └── ...
│   ├── Migrations/                 # EF migrations (auto-generated)
│   └── SeedData/                   # Dữ liệu seed khi init DB
├── Repositories/
│   ├── RepositoryBase.cs           # Generic repository — filter IsDeleted tự động
│   ├── RepositoryBaseDbContext.cs  # Variant dùng DbContext trực tiếp
│   ├── UnitOfWork.cs               # UoW implementation
│   ├── UnitOfWorkContext.cs
│   ├── UserRepository.cs
│   └── ...                         # Mỗi entity một file repository
├── Services/
│   ├── CloudinaryStorageService.cs # Upload/delete file trên Cloudinary
│   ├── GoogleEmailService.cs       # Gửi email qua Gmail SMTP
│   ├── FireBaseService.cs          # Push notification Firebase
│   ├── TokenProviderService.cs     # Generate/validate JWT + refresh token
│   ├── JobRegistrar.cs             # Đăng ký Hangfire jobs
│   ├── UserSessionCleanupJob.cs    # Job dọn session hết hạn
│   └── VerificationTokenCleanupJob.cs
├── Interceptors/
│   └── AuditSaveChangesInterceptor.cs  # Tự động ghi audit log khi SaveChanges
├── Constants/
│   ├── TableNames.cs               # Tên bảng DB (tránh magic strings)
│   └── CommonConstants.cs
└── DependencyInjection/
    └── Extentions/                 # Đăng ký tất cả Infrastructure services vào DI
```

---

## Quy tắc Repository

### Cấu trúc bắt buộc

```csharp
// Backend.Infrastructure/Repositories/SomeRepository.cs
public class SomeRepository : RepositoryBase<SomeEntity, int>, ISomeRepository
{
    private readonly BackendContext _context;

    public SomeRepository(BackendContext context, IUnitOfWork unitOfWork)
        : base(context, unitOfWork)
    {
        _context = context;
    }

    // Chỉ viết custom query ở đây
    // Mọi CRUD cơ bản đã có trong RepositoryBase
    public async Task<UserAggregates?> GetWithRolesAsync(int userId)
    {
        return await _context.Users
            .Where(x => !x.IsDeleted && x.Id == userId)
            .Select(x => new UserAggregates
            {
                Id = x.Id,
                Username = x.Username,
                Roles = x.UserRoles.Select(ur => ur.Role).ToList()
            })
            .FirstOrDefaultAsync();
    }
}
```

**Quy tắc Repository quan trọng:**

1. **`IsDeleted` filter** — `RepositoryBase` tự động thêm `Where(x => !x.IsDeleted)` trong tất cả method chuẩn. Khi tự viết query bằng `_context.Set<T>()`, phải thêm thủ công.

2. **`SaveChangesAsync`** — KHÔNG gọi trong repository. Gọi ở Service để đảm bảo transactional.

3. **AsNoTracking** — Dùng cho read-only query để tối ưu performance. `FindByCondition` với `trackChanges = false` sẽ tự dùng AsNoTracking.

4. **Transaction** — Dùng khi cần atomic operation nhiều repository:
   ```csharp
   // Trong Service
   await using var transaction = await _someRepository.BeginTransactionAsync();
   try {
       await _someRepository.CreateAsync(entity1);
       await _otherRepository.CreateAsync(entity2);
       await _someRepository.SaveChangesAsync();
       await transaction.CommitAsync();
   } catch {
       await transaction.RollbackAsync();
       throw;
   }
   ```

---

## Quy tắc EF Configuration (Fluent API)

```csharp
// Persistence/Configurations/SomeEntityConfiguration.cs
public class SomeEntityConfiguration : IEntityTypeConfiguration<SomeEntity>
{
    public void Configure(EntityTypeBuilder<SomeEntity> builder)
    {
        builder.ToTable(TableNames.SomeEntities);  // Dùng constant, không hardcode

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedOnAdd();

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        // Relationship
        builder.HasOne(x => x.User)
            .WithMany(x => x.SomeEntities)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);  // Không dùng Cascade Delete

        // Index
        builder.HasIndex(x => x.Name);
    }
}
```

**Quy tắc Fluent API:**
- **Không** dùng Data Annotations (`[Column]`, `[MaxLength]`, etc.) trên Entity
- Tên bảng đặt trong `Constants/TableNames.cs` — không hardcode string
- `OnDelete(DeleteBehavior.Restrict)` là default — tránh cascade delete gây mất data
- Luôn config `HasMaxLength` cho string fields để tránh nvarchar(MAX)
- Đăng ký Configuration trong `BackendContext.OnModelCreating` thông qua `ApplyConfigurationsFromAssembly`

---

## Quy tắc Migrations

```bash
# Tạo migration mới (chạy từ root solution)
dotnet ef migrations add MigrationName --project Backend.Infrastructure --startup-project Backend.API

# Apply migration
dotnet ef database update --project Backend.Infrastructure --startup-project Backend.API

# Rollback migration
dotnet ef database update PreviousMigrationName --project Backend.Infrastructure --startup-project Backend.API
```

**Quy tắc Migration:**
- Tên migration mô tả rõ thay đổi: `AddUserDeviceTable`, `AddIsActiveColumnToRole`
- **Không** edit file migration đã tạo — tạo migration mới để sửa
- Seed data đặt trong `Persistence/SeedData/` và gọi trong `InitialData` migration hoặc `DbContext.OnModelCreating`
- Backup DB trước khi apply migration trên production

---

## External Services

### CloudinaryStorageService
Upload file lên Cloudinary. Implement `IStorageService` từ Application.
- `UploadAsync(file, folder)` → trả về URL
- `DeleteAsync(publicId)` → xóa file

### GoogleEmailService
Gửi email qua Gmail SMTP. Implement `IEmailService<GoogleMailRequest>`.

### TokenProviderService
Generate JWT access token và refresh token.
- `GenerateToken(user, roles)` → `TokenResponse`
- `ValidateToken(token)` → claims

### FireBaseService
Gửi push notification qua FCM. Implement `IFireBaseService` từ Application.

---

## Thêm Repository mới

1. Tạo `IXxxRepository.cs` ở **Domain** (xem Domain README)
2. Tạo `XxxConfiguration.cs` ở `Persistence/Configurations/`
3. Đăng ký Configuration vào `BackendContext` (nếu chưa dùng `ApplyConfigurationsFromAssembly`)
4. Tạo `XxxRepository.cs` ở `Repositories/`
5. Đăng ký vào DI trong `DependencyInjection/Extentions/`:
   ```csharp
   services.AddScoped<IXxxRepository, XxxRepository>();
   ```
6. Tạo migration: `dotnet ef migrations add AddXxxTable ...`

---

## ⚠️ Lưu ý quan trọng

- **AuditSaveChangesInterceptor** tự động ghi `AuditLog` khi có Create/Update/Delete — không cần gọi thủ công
- `BackendContext` đã đăng ký interceptor này — mọi `SaveChangesAsync` đều qua đây
- Không inject `BackendContext` vào Service — chỉ inject Repository
- Hangfire jobs phải implement `IScheduledJob` và đăng ký trong `JobRegistrar`
