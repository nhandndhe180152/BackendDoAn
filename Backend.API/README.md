# Backend.API — Project Rules

> Layer trình bày (Presentation Layer). Đây là entry point của toàn bộ hệ thống.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Nhận HTTP request từ client và trả HTTP response
- Cấu hình toàn bộ DI container, middleware pipeline, Swagger, Auth, Rate Limiting
- **Không** chứa business logic — chỉ delegate sang Application layer

---

## Cấu trúc thư mục

```
Backend.API/
├── Controllers/        # HTTP endpoints — mỗi resource 1 file
├── Filters/            # Action filters (vd: BasicAuthDashboardAuthorizationFilter)
├── Middlewares/        # Global middleware (ExceptionHandling, TokenRevocation)
├── Utilities/          # Extensions cấu hình DI, helper cho controller
│   ├── ServiceExtensions.cs       # Đăng ký tất cả services vào DI
│   ├── ApplicationExtensions.cs   # Cấu hình middleware pipeline
│   ├── ConfigurationExtensions.cs # Đọc config từ appsettings
│   ├── ControllerHelper.cs        # Extension methods cho controller (GetLoggedInUserId)
│   ├── IBaseController.cs         # Interface chuẩn cho controller
│   ├── CustomAuthorizeAttribute.cs # Custom auth attribute theo Action enum
│   ├── RateLimitExtensions.cs     # Cấu hình rate limiting
│   └── ConfigureSwaggerOptions.cs # Cấu hình Swagger versioning
├── Configs/            # File config tĩnh (firebase-service.json)
├── Logs/               # Log files do Serilog tạo tự động (gitignore)
├── Properties/         # launchSettings.json
├── Program.cs          # Entry point — cấu hình builder và app
├── appsettings.json    # Config (KHÔNG commit credentials thật)
└── appsettings.Development.json
```

---

## Quy tắc Controller

### Cấu trúc bắt buộc

```csharp
[ApiVersion(1)]
[Route("api/v{version:apiVersion}/ten-resource")]  // kebab-case, số nhiều nếu là collection
[ApiController]
[Authorize]  // Bỏ nếu public endpoint
public class TenController : BaseController, IBaseController<int, CreateDto, UpdateDto, DTParameters>
{
    private readonly ITenService _service;

    public TenController(ITenService service) => _service = service;

    [HttpGet("{id}")]
    public async Task<IActionResult> GetByIdAsync(int id)
        => BaseResult(await _service.GetByIdAsync(id));
}
```

### Các HTTP method conventions

| Action | Method | Route | Body |
|--------|--------|-------|------|
| Lấy tất cả | GET | `/resource` | — |
| Lấy theo ID | GET | `/resource/{id}` | — |
| Paging cơ bản | POST | `/resource/paged` | `SearchQuery` |
| Paging nâng cao | POST | `/resource/paged-advanced` | `XxxDTParameters` |
| Tìm kiếm | POST | `/resource/all` | custom query |
| Tạo mới | POST | `/resource` | CreateDto |
| Cập nhật | PUT | `/resource` | UpdateDto |
| Xóa | DELETE | `/resource/{id}` | — |

### Lấy user hiện tại

```csharp
// Dùng extension method — KHÔNG tự decode JWT
var userId = this.GetLoggedInUserId();    // int
var username = this.GetLoggedInUserName(); // string
```

### Custom Authorization

```csharp
// Kiểm tra quyền theo Action enum trong Domain
[CustomAuthorize(Enums.Action.CREATE, Enums.Menu.USER)]
[HttpPost]
public async Task<IActionResult> CreateAsync([FromBody] CreateUserDto obj) { ... }
```

---

## Thêm Controller mới

1. Tạo file `Controllers/{TenEntity}Controller.cs`
2. Extends `BaseController`, implements `IBaseController<TKey, CreateDto, UpdateDto, DTParam>`
3. Inject service interface qua constructor
4. Tất cả method đều `return BaseResult(await _service.XxxAsync(...))`
5. **Không** cần đăng ký controller — ASP.NET tự discover

---

## Middleware pipeline (thứ tự quan trọng)

Xem `Utilities/ApplicationExtensions.cs`. Thứ tự hiện tại:
1. Exception Handling Middleware (bắt mọi unhandled exception)
2. Rate Limiting
3. HTTPS Redirection
4. CORS
5. Authentication → Authorization
6. Token Revocation Middleware (kiểm tra token bị thu hồi)
7. Swagger (chỉ ở Development)
8. Hangfire Dashboard
9. Health Checks
10. Controllers

---

## Cấu hình quan trọng trong `appsettings.json`

| Key | Mô tả |
|---|---|
| `ConnectionStrings.DefaultConnectionString` | MySQL connection string |
| `JwtSettings` | Secret key, issuer, audience, expire time |
| `CloudinarySettings` | File storage credentials |
| `SmtpSettings` | Gmail SMTP để gửi email |
| `HangfireSettings` | Background job dashboard config |
| `ScheduledJobs` | Bật/tắt và cron schedule của từng job |
| `CorsOrigins` | Whitelist domain được phép gọi API |
| `FireBase.ServicePath` | Đường dẫn đến firebase service account JSON |

---

## Lưu ý quan trọng

- Log file được Serilog tạo tự động tại `Logs/` — folder này phải được gitignore
- `appsettings.json` với credentials thật — **KHÔNG commit** lên git, chỉ dùng `appsettings.Development.json` locally hoặc biến môi trường trên server
- Migration chạy tự động khi startup (`app.ApplyMigrations()`) — cẩn thận ở môi trường production
- Cache được load sẵn khi startup (`app.LoadCacheAsync()`)
