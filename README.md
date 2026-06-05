# 🏗️ Backend API — Clean Architecture Documentation

> **Clean Architecture · .NET 8 · MySQL · ZeroTier**
>
> Tài liệu kỹ thuật toàn diện

| 🟢 .NET 8 | 🐬 MySQL 8 | 🔐 JWT + RBAC | 🌐 ZeroTier VPN |
|-----------|------------|---------------|-----------------|

---

## 1. Giới thiệu dự án

Backend API được xây dựng theo kiến trúc **Clean Architecture**, chia thành 5 project riêng biệt nhằm tách biệt rõ ràng các tầng trách nhiệm. Hệ thống cung cấp RESTful API cho ứng dụng quản lý nội dung, người dùng, thông báo, thanh toán và hơn thế nữa.

### Công nghệ chính

| Thành phần | Chi tiết |
|-----------|----------|
| Runtime | .NET 8 (ASP.NET Core) |
| Database | MySQL 8.x — truy cập qua ZeroTier VPN |
| ORM | Entity Framework Core (Code-First, Migrations) |
| Auth | JWT Bearer + RBAC (Role-Based Access Control) |
| Background Jobs | Hangfire + MySQL Storage |
| File Storage | Cloudinary (ảnh & tệp) |
| Push Notification | Firebase Cloud Messaging (FCM) |
| Email | Gmail SMTP (Google OAuth App Password) |
| Logging | Serilog — ghi file rolling theo ngày |
| Caching | In-Memory Cache (IMemoryCache) |
| API Docs | Swagger / OpenAPI với API Versioning |
| Image Processing | Magick.NET (ImageMagick wrapper) |
| Health Checks | ASP.NET HealthChecks + MySQL probe |
| Rate Limiting | ASP.NET Core RateLimiter (Fixed Window) |

---

## 2. Tổng quan kiến trúc (Clean Architecture)

Dự án áp dụng Clean Architecture với 5 project, phụ thuộc chỉ hướng vào trong (**Dependency Rule**). Layer ngoài có thể phụ thuộc layer trong, nhưng không chiều ngược lại.

| Thành phần | Chi tiết |
|-----------|----------|
|   🌐  Backend.API  (Presentation Layer)   |
|   ⚙️  Backend.Application  (Business Logic Layer)   |
|   🏛️  Backend.Domain  (Domain / Core Layer)   |
|   🔧  Backend.Infrastructure  (Infrastructure Layer)   |
|   📦  Backend.Share  (Cross-cutting Concerns)   |


> **📌 Quy tắc phụ thuộc:** `API → Application → Domain ← Infrastructure` | `Share` được tất cả các layer sử dụng. `Domain` không phụ thuộc vào bất kỳ layer nào khác.

---

## 3. Chi tiết từng project

### 3.1 Backend.API — Presentation Layer

Là điểm vào duy nhất của hệ thống. Chứa các Controller, Middleware, Filter và toàn bộ cấu hình pipeline HTTP. Project này chỉ phụ thuộc vào `Backend.Application` và `Backend.Share`.

| 📁 Thư mục / File | Mô tả |
|------------------|-------|
| `Controllers/` | Các lớp Controller xử lý HTTP request. Mỗi controller kế thừa `BaseController` và dùng `IBaseController` interface. |
| `Middlewares/` | Xử lý cross-cutting concerns trong pipeline HTTP. |
| `Filters/` | Action Filter dùng cho Hangfire Dashboard authentication. |
| `Utilities/` | Các extension method đăng ký service và cấu hình app. |
| `Configs/` | Chứa file cấu hình ngoài như `firebase-service.json`. |
| `Logs/` | Thư mục chứa file log sinh ra bởi Serilog (rolling theo ngày). |
| `Properties/` | `launchSettings.json` — cấu hình profile chạy local. |
| `Program.cs` | Entry point: khởi tạo Serilog, đăng ký service, build & run app. |
| `appsettings.json` | Cấu hình tổng: ConnectionString, JWT, SMTP, Cloudinary, Firebase, CORS, Kestrel… |
| `appsettings.Development.json` | Override cấu hình riêng cho môi trường Development. |
| `Backend.API.csproj` | Khai báo dependencies NuGet của project API. |

#### Controllers quan trọng

| Controller | Mô tả |
|-----------|-------|
| `AuthController.cs` | Đăng nhập, đăng ký, refresh token, đổi mật khẩu, quên mật khẩu, đăng xuất. |
| `UserController.cs` | CRUD người dùng, phân trang, tìm kiếm, xuất Excel, upload avatar. |
| `RoleController.cs` | Quản lý Role và phân quyền (Permission) theo Menu + Action. |
| `NotificationController.cs` | Gửi & quản lý thông báo push (Firebase), lấy danh sách, đánh dấu đã đọc. |
| `BlogPostController.cs` | CRUD bài viết blog, duyệt bài, phân trang, filter theo category/tag. |
| `FileManagerController.cs` | Upload file/ảnh lên Cloudinary, quản lý thư mục, xoá file. |
| `PaymentTransactionController.cs` | Tra cứu giao dịch thanh toán theo bộ lọc, phân trang. |
| `SystemConfigController.cs` | Đọc/ghi cấu hình hệ thống động (key-value) từ database. |
| `AuditLogController.cs` | Tra cứu lịch sử thay đổi dữ liệu (ai thay đổi gì, khi nào). |
| `DashboardController.cs` | Endpoint trả về dữ liệu thống kê cho trang Dashboard. |

#### Middlewares

| Middleware | Mô tả |
|-----------|-------|
| `TokenRevocationMiddleware.cs` | Chạy trước mọi request có `Authorization` header. Kiểm tra JWT có bị thu hồi (`UserSession.IsRevoked`) không, và trạng thái tài khoản có bị khoá không. Trả `401`/`403` ngay nếu vi phạm. |
| `ExceptionHandlingMiddleware.cs` | Bắt toàn bộ exception chưa được xử lý, trả về JSON chuẩn `ApiResponse` với status 500. Đồng thời ghi `ActivityLog` cho mỗi request (method + path + status code). |

#### Utilities

| File | Mô tả |
|------|-------|
| `ServiceExtensions.cs` | Đăng ký: CORS, Controllers, Swagger + JWT Security, API Versioning, JWT Authentication, Kestrel limits (50MB), Firebase, Rate Limiter, HttpClient. |
| `ApplicationExtensions.cs` | Cấu hình pipeline: Swagger UI, Static Files, HTTPS redirect, CORS, Auth, RateLimiter, Hangfire Dashboard, Health Checks, MapControllers. |
| `ConfigurationExtensions.cs` | Extension `AddAppConfigurations()` — nạp các Options từ appsettings. |
| `RateLimitExtensions.cs` | Khai báo 3 policy rate limit: `StrictLoginPolicy` (5 req/phút — cho login), `GeneralPolicy` (100 req/phút), `GlobalLimiter` (200 req/phút — cho toàn bộ IP). |
| `CustomAuthorizeAttribute.cs` | Attribute `[CustomAuthorize(Menu, Action)]` — kiểm tra quyền RBAC dựa trên `UserRole → Permission → Menu + Action`. Hỗ trợ cache permission. |
| `ControllerHelper.cs` | Helper lấy thông tin user hiện tại từ `HttpContext` claims. |
| `IBaseController.cs` | Interface chuẩn hoá response type cho tất cả Controller. |
| `ConfigureSwaggerOptions.cs` | Cấu hình Swagger tự động theo API version (v1, v2…). |

---

### 3.2 Backend.Application — Business Logic Layer

Chứa toàn bộ business logic của hệ thống. Định nghĩa Interface cho các service, implement các service cụ thể, khai báo DTO, Mapping, Validator và hằng số nghiệp vụ. Layer này **không phụ thuộc vào Infrastructure hay API**.

| 📁 Thư mục / File | Mô tả |
|------------------|-------|
| `Interfaces/` | Định nghĩa contract cho tất cả service (`IAuthService`, `IUserService`…). Đây là abstraction layer — Infrastructure implement, API tiêu thụ. |
| `Implements/` | Cài đặt cụ thể các service. Mỗi service inject các `IRepository` tương ứng qua constructor. |
| `DTOs/` | Data Transfer Objects — các class Request/Response cho từng use case. Chia theo domain (`Users/`, `BlogPosts/`, `Auths/`…). |
| `Mappings/` | AutoMapper Profile — map giữa `Entity ↔ DTO` cho từng domain. |
| `Validators/` | FluentValidation Validator — validate dữ liệu đầu vào (Request DTO) theo từng use case. |
| `Constants/` | Hằng số nghiệp vụ: mã lỗi (`ApiCodeConstants`), thông báo lỗi (`ErrorMessagesConstants`), cache key, notification template, v.v. |
| `EmailTemplates/` | File HTML template email: đăng ký tài khoản, quên mật khẩu (admin & client), OTP. |
| `StaticFiles/` | Dữ liệu tĩnh JSON: danh sách tỉnh/thành (`provinces.json`), quận/huyện, phường/xã (`wards.json`) — dùng để seed database. |
| `DependencyInjection/` | Extension `AddApplicationServices()` — đăng ký tất cả service vào DI container. |
| `Backend.Application.csproj` | Khai báo NuGet: AutoMapper, FluentValidation, MediatR (nếu có), v.v. |

#### Các service cốt lõi

| Service | Mô tả |
|--------|-------|
| `AuthService.cs` | Xử lý toàn bộ luồng xác thực: đăng nhập (JWT + Refresh Token), đăng ký, xác thực email OTP, đổi mật khẩu, quên mật khẩu, đăng xuất (thu hồi token). Đây là service lớn nhất (~41KB). |
| `UserService.cs` | CRUD user, phân trang DataTable, upload avatar, đổi trạng thái, xuất Excel, quản lý thiết bị (`UserDevice`). |
| `RoleService.cs` | Quản lý Role, Permission. Đồng bộ quyền hàng loạt (bulk save permissions theo menu/action). |
| `NotificationService.cs` | Tạo & gửi thông báo: push FCM tới thiết bị, lưu DB, đánh dấu đọc, phân trang. |
| `BlogPostService.cs` | CRUD bài viết: soạn thảo, gán category/tag, duyệt bài, lọc theo status/category. |
| `FileUploadService.cs` | Upload file đơn/hàng loạt lên Cloudinary, kiểm tra loại file, resize ảnh, xoá file. |
| `SystemConfigService.cs` | CRUD cấu hình hệ thống (key-value) — cho phép thay đổi config động mà không cần restart. |
| `ProvinceService.cs` & `WardService.cs` | Import/tra cứu dữ liệu tỉnh thành, quận huyện, phường xã từ `StaticFiles`. |
| `PaymentTransactionService.cs` | Ghi nhận & truy vấn giao dịch thanh toán. |
| `MenuService.cs` | Quản lý cấu trúc Menu phân cấp, gán Action vào Menu. |

#### Constants quan trọng

| File | Mô tả |
|------|-------|
| `ApiCodeConstants.cs` | Định nghĩa mã code API (vd: `Auth.InvalidToken`, `Common.Forbidden`…) — trả về trong `ApiResponse.Code` để client xử lý phân loại lỗi. |
| `ErrorMessagesConstants.cs` | Map mã code → thông báo lỗi tiếng Việt/Anh. Dùng `GetMessage(code)` để lấy message. |
| `CommonConstants.cs` | Cache key, độ dài chuỗi tối đa, regex pattern, ActivityLog type. |
| `NotificationConstants.cs` | Template tiêu đề & nội dung thông báo FCM theo loại sự kiện. |
| `AuthConstants.cs` | Thời gian hết hạn token, loại token, giá trị mặc định. |
| `AuditLogConstants.cs` | Danh sách action được audit (Created/Modified/Deleted). |
| `FileManagerConstants.cs` | Giới hạn dung lượng file, danh sách extension được phép upload. |

---

### 3.3 Backend.Domain — Core / Domain Layer

Tầng nhân lõi — **không phụ thuộc vào bất kỳ layer nào khác**. Chứa Entity, Enum, Abstraction base class và Interface Repository. Mọi business rule cốt lõi đều nằm ở đây.

| 📁 Thư mục / File | Mô tả |
|------------------|-------|
| `Entities/` | Các class Entity map trực tiếp với bảng database (EF Core). Kế thừa `EntityBase` hoặc `EntityAuditBase`. |
| `Abstractions/` | Base class & interface nền tảng: `EntityBase`, `EntityAuditBase`, `IRepositoryBase`, `IUnitOfWork`. |
| `Aggregates/` | Aggregate — projection class dùng cho query phức tạp, join nhiều bảng. Không map trực tiếp DB. |
| `DTParameters/` | Parameter class cho DataTable server-side (search, sort, paging, filter) theo từng domain. |
| `Enums/` | Enum nghiệp vụ: `UserStatus`, `Role`, `Menu`, `Action`, `Gender`, `PaymentStatus`, `PaymentMethod`. |
| `Commons/` | `DTResultWithSummary` — kiểu dữ liệu trả về có tổng hợp (tổng tiền, tổng bản ghi…). |
| `Interfaces/` | Interface Repository cho từng Entity (`IUserRepository`, `IBlogPostRepository`…). |
| `Backend.Domain.csproj` | Không có NuGet dependency bên ngoài ngoài EF Core abstractions. |

#### Abstraction Base Classes

| Class / Interface | Mô tả |
|------------------|-------|
| `EntityBase<TKey>` | Base nhỏ nhất: `Id` (generic key) + `IsDeleted` (soft delete flag). Mọi entity đều kế thừa class này. |
| `EntityAuditBase<TKey>` | Kế thừa `EntityBase`, bổ sung: `CreatedDate`, `LastModifiedDate`, `CreatedBy`, `UpdatedBy` — dùng cho entity cần audit trail. |
| `EntityCommonBase` | Bổ sung thêm `Name`, `Description`, `IsActive` — dùng cho các entity danh mục. |
| `EntityFullTextSearch` | Bổ sung `SearchVector` — hỗ trợ full-text search. |
| `IUnitOfWork` | Interface Unit of Work: `SaveChangesAsync()` — đảm bảo tính atomicity của transaction. |
| `IUnitOfWorkContext<T>` | Generic Unit of Work gắn với DbContext cụ thể. |

#### Entities chính

| Entity | Mô tả |
|--------|-------|
| `User.cs` | Thông tin người dùng: Email, FullName, Phone, Avatar, UserStatusId, các FK liên quan. |
| `UserSession.cs` | Session JWT: `AccessTokenJti`, `RefreshToken`, `IsRevoked`, `ExpiryDate`, `DeviceInfo`. |
| `UserRole.cs` | Mapping User ↔ Role (many-to-many). |
| `Permission.cs` | Mapping Role ↔ Menu ↔ Action — bảng phân quyền. |
| `BlogPost.cs` | Bài viết blog: Title, Content, Slug, ThumbnailUrl, BlogPostStatusId, BlogLayoutId… |
| `Notification.cs` | Thông báo: Title, Body, NotificationTypeId, NotificationCategoryId, IsRead… |
| `FileUpload.cs` | Metadata file upload: FileName, Url, FolderUploadId, FileType, Size. |
| `AuditLog.cs` | Lịch sử thay đổi dữ liệu: TargetType, TargetId, Action, DataBefore, DataAfter (JSON), IpAddress. |
| `ActivityLog.cs` | Log HTTP request: Method, Path, StatusCode, UserId, IpAddress, UserAgent. |
| `PaymentTransaction.cs` | Giao dịch: Amount, PaymentMethodId, PaymentStatusId, Reference… |
| `SystemConfig.cs` | Cấu hình key-value động: Key, Value, Description. |

---

### 3.4 Backend.Infrastructure — Infrastructure Layer

Implement cụ thể các interface được định nghĩa ở Domain và Application. Chứa EF Core DbContext, Repository, Migration, External Service và Background Job. Layer này biết về Database, Cloud, Email — Domain và Application không biết.

| 📁 Thư mục / File | Mô tả |
|------------------|-------|
| `Persistence/` | DbContext, EntityTypeConfiguration, Migration, SeedData. |
| `Repositories/` | Implement các `IRepository` — truy vấn database qua EF Core. |
| `Services/` | External services: Email (Gmail SMTP), Firebase FCM, Cloudinary, JWT Token Provider, Hangfire Jobs. |
| `Interceptors/` | EF Core Interceptor: tự động ghi AuditLog khi SaveChanges. |
| `DependencyInjection/` | Extension `AddInfrastructureServices()` — đăng ký DbContext, Repository, Service, Hangfire, HealthChecks vào DI. |
| `Constants/` | `TableNames.cs` (tên bảng DB), `CommonConstants.cs` (danh sách entity được audit). |
| `Backend.Infrastructure.csproj` | NuGet: EF Core MySQL, Hangfire.MySql, Cloudinary, Firebase, Serilog, HealthChecks. |

#### Persistence

| File | Mô tả |
|------|-------|
| `BackendContext.cs` | DbContext chính: khai báo tất cả `DbSet<>`, cấu hình DB function `DATE_FORMAT` (MySQL), nạp SeedData. |
| `Configurations/` | `IEntityTypeConfiguration<T>` cho từng Entity: tên bảng (TableNames), index, relationship, column type, unique constraint. |
| `Migrations/` | File migration EF Core — lịch sử thay đổi schema database. Tự động chạy khi khởi động (`ApplyMigrations()`). |
| `SeedData/` | Dữ liệu mồi ban đầu: `ActionSeed` (CRUD/EXPORT/APPROVE), `RoleSeed` (ADMIN/USER), `UserSeed` (admin mặc định), `UserStatusSeed`, `UserRoleSeed`. |

#### Repositories

| File | Mô tả |
|------|-------|
| `RepositoryBase<TEntity,TKey>` | Generic Repository: GetAll, FindByCondition, CreateAsync, UpdateAsync, DeleteAsync, CountByCondition, BeginTransaction, v.v. Tự động lọc `IsDeleted = false` (soft delete). |
| `RepositoryBaseDbContext<T,TEntity,TKey>` | Generic Repository nhận DbContext qua generic type — dùng khi cần truy cập context cụ thể. |
| `UnitOfWork.cs` | Implement `IUnitOfWork`: gọi `dbContext.SaveChangesAsync()`. |
| `UnitOfWorkContext<T>.cs` | Generic UnitOfWork — wrap SaveChanges cho context cụ thể. |
| `[Domain]Repository.cs` | Repository cụ thể cho từng Entity — override hoặc bổ sung query phức tạp (join, stored procedure, raw SQL). |

#### Services

| Service | Mô tả |
|--------|-------|
| `TokenProviderService.cs` | Tạo và parse JWT Access Token + Refresh Token. Dùng HS256, khai báo claims (userId, roleId, jti…). |
| `CloudinaryStorageService.cs` | Upload/xoá file trên Cloudinary. Hỗ trợ ảnh (auto-transform) và file thường. |
| `GoogleEmailService.cs` | Gửi email qua Gmail SMTP (App Password). Hỗ trợ HTML template. |
| `FireBaseService.cs` | Gửi push notification qua Firebase Admin SDK (FCM). Hỗ trợ gửi đơn & hàng loạt. |
| `UserSessionCleanupJob.cs` | Hangfire Job — chạy mỗi 6 tiếng: xoá UserSession hết hạn. |
| `VerificationTokenCleanupJob.cs` | Hangfire Job — chạy mỗi 30 phút: xoá VerificationToken hết hạn. |
| `JobRegistrar.cs` | Đăng ký các Recurring Job vào Hangfire theo cấu hình Cron từ appsettings. |
| `IScheduledJob` / `IJobRegistrar` | Interface để mock/test background job. |

#### Interceptors

| File | Mô tả |
|------|-------|
| `AuditSaveChangesInterceptor.cs` | Override `SavingChangesAsync()` của EF Core. Tự động tạo bản ghi `AuditLog` cho các entity trong danh sách `CommonConstants.AuditedEntityNames` mỗi khi có thay đổi (Added/Modified/Deleted). Ghi `DataBefore` & `DataAfter` dạng JSON, IpAddress, UserAgent, UserId. |

---

### 3.5 Backend.Share — Cross-cutting Concerns

Thư viện dùng chung (shared kernel) — được tất cả các layer khác tham chiếu. Không chứa business logic, chỉ chứa utility, helper, extension method và contract dùng chung.

| 📁 Thư mục / File | Mô tả |
|------------------|-------|
| `Entities/` | Model dùng chung: `ApiResponse`, `PagingData`, `DataTableModel`, `SearchQuery`, `ApexChartData`, `FileUploadResult`, `GoogleMailRequest`… |
| `Extensions/` | Extension methods cho DateTime, String, IEnumerable, IQueryable, HttpContext, HttpClient, ImageProcessor. |
| `Helpers/` | Static helper: `EmailHelper`, `PhoneHelper`, `PasswordHelper` (BCrypt), `RandomHelper`, `StringHelper`, `CronHelper`, `FileHelper`. |
| `Services/` | Interface + Implement service nhỏ: `ICacheService`/`MemoryCacheService`, `ISerializeService`/`SerializeService`, `IImageProcessor`/`MagickImageProcessor`, `IScheduledJobService`/`ScheduledJobService`. |
| `Constants/` | `ClaimNames.cs` (tên claim trong JWT), `SQLParams.cs` (param name SQL). |
| `Enums/` | `CommonEnum.cs` — enum chung không thuộc domain cụ thể. |
| `Attributes/` | `SensitiveDataAttribute` (đánh dấu field nhạy cảm, bỏ qua khi serialize log), `SortTypeValidateAttribute` (validate sort direction). |
| `Backend.Share.csproj` | NuGet: Magick.NET, Newtonsoft.Json, BCrypt.Net, v.v. |

#### Extension Methods nổi bật

| File | Mô tả |
|------|-------|
| `DateTimeExtensions.cs` | `ToVietnameseDateTime()`, `ToVietnameseDateOffset()`, `ToVietnameseDate()` — format ngày giờ kiểu Việt Nam. Map sang DB Function `DATE_FORMAT` của MySQL qua EF Core `HasDbFunction`. |
| `StringExtensions.cs` | `ToSlug()` (tạo URL-friendly slug), `RemoveVietnameseDiacritics()`, `ToSnakeCase()`, `MaskEmail()`, `MaskPhone()`… |
| `HttpContextExtensions.cs` | `GetCurrentUserId()` — lấy userId từ JWT claim, `GetRemoteHostIpAddress()` — lấy IP thật (hỗ trợ X-Forwarded-For). |
| `LinqExtensions.cs` | `WhereIf()` — áp dụng điều kiện có điều kiện, tránh if/else verbose. |
| `HttpClientExtensions.cs` | `GetAsync<T>`, `PostAsync<T>` — wrapper typed cho HttpClient với JSON deserialization. |

#### ApiResponse — Chuẩn hoá response

Tất cả endpoint đều trả về `ApiResponse<T>`. Cấu trúc:

```json
{
  "isSucceeded": true,
  "status": 200,
  "code": "SUCCESS",
  "message": "Thao tác thành công",
  "resources": { /* data */ },
  "errors": null
}
```

| Method | HTTP Status | Mô tả |
|--------|------------|-------|
| `ApiResponse.Ok(data)` | 200 | Thành công, có data |
| `ApiResponse.Created(data)` | 201 | Tạo mới thành công |
| `ApiResponse.BadRequest(msg, code)` | 400 | Dữ liệu đầu vào sai |
| `ApiResponse.Unauthorized(msg, code)` | 401 | Chưa xác thực |
| `ApiResponse.Forbidden(msg, code)` | 403 | Không có quyền |
| `ApiResponse.NotFound(msg, code)` | 404 | Không tìm thấy |
| `ApiResponse.Error(msg, status, code)` | 500 | Lỗi server |

---

## 4. Luồng dữ liệu (Request Flow)

Một HTTP request điển hình đi qua các tầng theo thứ tự sau:

```
HTTP Request
   │
   ▼  Rate Limiter (RateLimitExtensions)
   │
   ▼  TokenRevocationMiddleware
   │       └─ Kiểm tra JWT có bị thu hồi không
   │       └─ Kiểm tra trạng thái tài khoản
   │
   ▼  ExceptionHandlingMiddleware
   │       └─ Bắt exception, ghi ActivityLog
   │
   ▼  Authentication (JWT Bearer)
   │
   ▼  Authorization ([CustomAuthorize(Menu, Action)])
   │       └─ Kiểm tra RBAC permission
   │
   ▼  Controller Action
   │       └─ Validate Request (FluentValidation)
   │       └─ Gọi IService.MethodAsync(dto)
   │
   ▼  Service (Application Layer)
   │       └─ Business logic
   │       └─ Gọi IRepository
   │
   ▼  Repository (Infrastructure Layer)
   │       └─ EF Core query → MySQL
   │       └─ AuditSaveChangesInterceptor (tự động)
   │
   ▼  HTTP Response (ApiResponse<T>)
```

---

## 5. Cấu hình hệ thống (appsettings.json)

#### ConnectionStrings

```json
"ConnectionStrings": {
  "DefaultConnectionString": "Server=<IP_ZeroTier>;Port=3306;Database=backend;User=user_login;Password=<PASSWORD>;SslMode=Required;AllowPublicKeyRetrieval=True;"
}
```

#### JwtSettings

```json
"JwtSettings": {
  "SecretKey": "<32+ ký tự ngẫu nhiên>",
  "Issuer": "BackendApi",
  "Audience": "BackendClient",
  "ExpireTime": 1,        // Access token hết hạn sau N ngày
  "RefreshTokenTtl": 1    // Refresh token hết hạn sau N ngày
}
```

#### HangfireSettings

```json
"HangfireSettings": {
  "Route": "/jobs",        // URL dashboard: http://host/jobs
  "ServerName": "Backend_Server",
  "Dashboard": {
    "Username": "admin",   // Basic auth Hangfire dashboard
    "Password": "Abc@123456"
  },
  "ConnectionString": "..."  // Dùng cùng DB, thêm Allow User Variables=true
}
```

#### ScheduledJobs

```json
"ScheduledJobs": {
  "CleanupUserSession":        { "Enabled": true, "Cron": "0 */6 * * *" },   // mỗi 6h
  "CleanupVerificationTokens": { "Enabled": true, "Cron": "*/30 * * * *" },  // mỗi 30 phút
  "CreateDriverSalaries":      { "Enabled": true, "Cron": "0 0 25 * *" }     // ngày 25 hàng tháng
}
```

#### CloudinarySettings

```json
"CloudinarySettings": {
  "CloudName": "<your_cloud_name>",
  "ApiKey": "<your_api_key>",
  "ApiSecret": "<your_api_secret>"
}
```

---

## 6. Kết nối MySQL từ xa qua ZeroTier VPN

> **🌐 ZeroTier là gì?**
> ZeroTier tạo một mạng LAN ảo (Virtual Private Network) ngang hàng (peer-to-peer) giữa các thiết bị. Máy laptop ở nhà và server chạy MySQL sẽ cùng nằm trong một mạng ảo riêng, kết nối trực tiếp mà không cần mở cổng public hay cấu hình NAT phức tạp.

### 6.1 Kiến trúc kết nối

```
┌──────────────────────┐          ZeroTier Network          ┌──────────────────────┐
│   Laptop ở nhà       │◄──────────────────────────────────►│   Server MySQL       │
│   (Dev machine)      │    IP ảo ZeroTier: 172.25.x.x      │   (Ubuntu/VPS)       │
│                      │    (mạng riêng, mã hóa E2E)        │   Port 3306 (MySQL)  │
│  - IDE / DBeaver     │                                     │   Port 3306 chỉ bind │
│  - dotnet run        │                                     │   0.0.0.0 hoặc ZT IP │
└──────────────────────┘                                     └──────────────────────┘

  ConnectionString: Server=172.25.55.18;Port=3306;Database=backend;...
                              ↑
                    IP ZeroTier của server MySQL
```

### 6.2 Cài đặt ZeroTier trên Server (Linux/Ubuntu)

```bash
# Cài ZeroTier
curl -s https://install.zerotier.com | sudo bash

# Join vào network (lấy Network ID từ my.zerotier.com)
sudo zerotier-cli join <NETWORK_ID>
# Ví dụ: sudo zerotier-cli join 8056c2e21c000001

# Kiểm tra trạng thái và lấy IP ZeroTier
sudo zerotier-cli status
sudo zerotier-cli listnetworks
# Hoặc xem IP được gán:
ip addr show zt+
```

Vào **ZeroTier Central** (`my.zerotier.com`) → Network → Members → tick **Authorize** cho server.

```bash
# Đảm bảo MySQL bind đúng address
# /etc/mysql/mysql.conf.d/mysqld.cnf
bind-address = 0.0.0.0
# Hoặc chỉ bind ZeroTier IP:
bind-address = 172.25.55.18

sudo systemctl restart mysql
```

### 6.3 Cài đặt ZeroTier trên Laptop

#### macOS
- Tải ZeroTier One: https://www.zerotier.com/download/
- Mở ứng dụng → Join Network → nhập Network ID → click Join.
- Vào ZeroTier Central → authorize máy laptop.

```bash
ping 172.25.55.18      # ping IP ZeroTier của server
mysql -h 172.25.55.18 -P 3306 -u user_login -p
```

#### Windows
- Tải installer `.msi` từ https://www.zerotier.com/download/
- Cài đặt → chuột phải icon ZeroTier trong system tray → **Join New Network**.
- Nhập Network ID → authorize trên ZeroTier Central.
- Kiểm tra IP được gán: Settings → Network Connections → ZeroTier adapter.

### 6.4 Tạo MySQL User cho kết nối ZeroTier

Thực hành bảo mật tốt: tạo user riêng chỉ cho phép kết nối từ dải IP ZeroTier.

```sql
-- Kết nối MySQL với root
mysql -u root -p

-- Tạo user cho phép từ toàn bộ ZeroTier subnet
CREATE USER 'user_login'@'172.25.%'
  IDENTIFIED BY 'S75p#qL!2026@vN_uLtr4';

-- Cấp quyền trên database backend
GRANT ALL PRIVILEGES ON backend.* TO 'user_login'@'172.25.%';

FLUSH PRIVILEGES;
```

> **🔐 Bảo mật:** Chỉ cho phép kết nối từ dải `172.25.%` (subnet ZeroTier). Không mở MySQL ra internet public. Dùng `SslMode=Required` trong connection string để mã hóa kết nối.

### 6.5 Cấu hình Connection String trong appsettings.json

```json
"ConnectionStrings": {
  "DefaultConnectionString":
    "Server=172.25.55.18;Port=3306;Database=backend;User=user_login;Password=<YOUR_PASSWORD>;SslMode=Required;AllowPublicKeyRetrieval=True;"
}
```

### 6.6 Mở cổng firewall trên Server (UFW)

```bash
# Cho phép MySQL từ ZeroTier subnet
sudo ufw allow from 172.25.0.0/16 to any port 3306

# Kiểm tra rule
sudo ufw status verbose

# KHÔNG làm điều này (nguy hiểm):
# sudo ufw allow 3306  ← mở toàn bộ internet!
```

### 6.7 Kiểm tra kết nối từ laptop

```bash
# 1. Ping kiểm tra network ZeroTier
ping 172.25.55.18

# 2. Kiểm tra cổng MySQL mở
telnet 172.25.55.18 3306
# hoặc:
nc -zv 172.25.55.18 3306

# 3. Kết nối MySQL CLI
mysql -h 172.25.55.18 -P 3306 -u user_login -p backend

# 4. Chạy ứng dụng .NET từ laptop
dotnet run --project Backend.API
# → EF Core sẽ kết nối MySQL qua ZeroTier tự động
```

### 6.8 Troubleshooting

| Vấn đề | Cách xử lý |
|--------|-----------|
| Không ping được `172.25.x.x` | Kiểm tra ZeroTier đã join network chưa? Máy đã được authorize trên Central chưa? |
| Ping được nhưng MySQL từ chối kết nối | Kiểm tra MySQL `bind-address`, UFW firewall, user `'user_login'@'172.25.%'` đã tồn tại chưa. |
| SSL error khi connect | Thêm `SslMode=None` (chỉ dùng khi test nội bộ, không dùng production) hoặc cấu hình SSL certificate đúng. |
| ZeroTier status: DISCONNECTED | Restart ZeroTier: `sudo systemctl restart zerotier-one`. Kiểm tra internet server. |
| Connection timeout từ .NET | Tăng `Connection Timeout` trong connection string: `Connection Timeout=30` |
| EF Migration fail | Chạy: `dotnet ef database update --project Backend.Infrastructure --startup-project Backend.API` |

---

## 7. Hướng dẫn chạy dự án

### 7.1 Yêu cầu môi trường

| Thành phần | Yêu cầu |
|-----------|---------|
| .NET SDK | 8.0 trở lên — https://dotnet.microsoft.com/download |
| MySQL | 8.x (cài local hoặc kết nối qua ZeroTier) |
| ZeroTier | Đã join network và authorize (nếu DB ở server xa) |
| Cloudinary Account | Tạo free account tại cloudinary.com |
| Firebase Project | Tạo project, tải `firebase-service.json` |
| Gmail App Password | Bật 2FA Gmail → tạo App Password |

### 7.2 Các bước chạy

**1.** Clone hoặc giải nén source code vào thư mục dự án.

**2.** Cập nhật `appsettings.json` với thông tin thực tế (ConnectionString, Cloudinary, Firebase, Gmail).

**3.** Đặt file `firebase-service.json` vào `Backend.API/Configs/`

**4.** Chạy migration (tạo database schema):

```bash
dotnet ef database update \
  --project Backend.Infrastructure \
  --startup-project Backend.API
```

**5.** Chạy ứng dụng:

```bash
cd Backend.API
dotnet run

# Hoặc chạy ở môi trường cụ thể:
dotnet run --environment Development
```

**6.** Mở trình duyệt truy cập:
- **Swagger UI:** https://localhost:7156/swagger
- **Hangfire Dashboard:** https://localhost:7156/jobs *(username: admin, password: xem appsettings.json)*
- **Health Check:** https://localhost:7156/health/ready

### 7.3 Endpoints đặc biệt

| Endpoint | Mô tả |
|---------|-------|
| `/swagger` | Swagger UI — tài liệu API tự động, hỗ trợ JWT Bearer test |
| `/jobs` | Hangfire Dashboard — xem & quản lý background job |
| `/health/live` | Liveness probe — kiểm tra app còn sống không |
| `/health/ready` | Readiness probe — kiểm tra DB connection và các dependency |
| `/uploads/users/avatars/*` | Static file — truy cập avatar người dùng |
| `/uploads/blog-posts/*` | Static file — truy cập ảnh bài viết |

---

## 8. Bảo mật & Phân quyền (RBAC)

### 8.1 JWT Authentication Flow

```
POST /api/v1/auth/login
  → Validate credentials
  → Tạo AccessToken (JWT, exp = 1 ngày) + RefreshToken
  → Lưu UserSession (AccessTokenJti, RefreshToken, IsRevoked=false)
  → Trả về { accessToken, refreshToken }

Mỗi request tiếp theo:
  Authorization: Bearer <accessToken>
  → TokenRevocationMiddleware kiểm tra JTI trong UserSession
  → Nếu IsRevoked=true → 401 Unauthorized

POST /api/v1/auth/refresh-token
  → Validate refreshToken
  → Revoke session cũ (IsRevoked=true)
  → Tạo AccessToken + RefreshToken mới

POST /api/v1/auth/logout
  → Đặt IsRevoked=true cho session hiện tại
```

### 8.2 RBAC — Phân quyền theo Menu & Action

Hệ thống dùng mô hình phân quyền 3 tầng: `User → Role → Permission (Menu + Action)`.

```
User ──has many──► UserRole ──► Role
                                 └──has many──► Permission
                                                   ├── MenuId  (vd: BLOG_POST = 1003)
                                                   └── ActionId (vd: CREATE = 1001)
```

```csharp
// Dùng trên Controller/Action:
[CustomAuthorize(Enums.Menu.BLOG_POST, Enums.Action.CREATE)]
public async Task<IActionResult> CreatePost([FromBody] CreateBlogPostRequest req)
```

### 8.3 Rate Limiting

| Policy | Giới hạn | Áp dụng |
|--------|---------|---------|
| `StrictLoginPolicy` | 5 request / phút / IP | Endpoint đăng nhập (chống brute force) |
| `GeneralPolicy` | 100 request / phút / IP | Các endpoint thông thường |
| `Global Limiter` | 200 request / phút / IP | Giới hạn tổng toàn bộ API |
| Health Check | Bypass rate limit | Luôn trả về 200 |

---

## 9. Hệ thống Logging

Dự án dùng Serilog với 2 sink file riêng biệt, rolling theo ngày:

| Sink | Mô tả |
|------|-------|
| `Logs/info-log{date}.txt` | Ghi INFO và WARN. Lọc bỏ log của EF Core database command (tránh spam query log). Rolling interval: ngày. |
| `Logs/error-log{date}.txt` | Chỉ ghi ERROR trở lên. Dùng để debug lỗi production. Rolling interval: ngày. |
| Console | Ghi tất cả level, hiển thị realtime khi chạy local. |
| Serilog Request Logging | `app.UseSerilogRequestLogging()` — tự động log HTTP request (method, path, status code, thời gian xử lý). |

---

## 10. Cấu trúc Database

Tất cả tên bảng được khai báo tập trung trong `TableNames.cs`. Entity Framework Core quản lý schema qua Migrations.

### Nhóm User & Auth

| Bảng | Mô tả |
|------|-------|
| `User` | Thông tin người dùng chính |
| `UserStatus` | Trạng thái: NotActivated, Actived, Locked, Deactivated |
| `UserRole` | Many-to-many: User ↔ Role |
| `UserSession` | JWT session: AccessTokenJti, RefreshToken, IsRevoked |
| `UserVerificationToken` | Token xác thực email (đăng ký, quên mật khẩu) |
| `UserDevice` | Thiết bị đăng nhập (FCM token, platform, device fingerprint) |
| `UserNotification` | Mapping: User ↔ Notification (đã đọc chưa) |

### Nhóm RBAC

| Bảng | Mô tả |
|------|-------|
| `Role` | ADMIN, USER… |
| `Action` | CREATE, READ, UPDATE, DELETE, EXPORT, APPROVE |
| `Menu` | DASHBOARD, BLOG_POST, USER, ROLE, SYSTEM_SETTINGS… |
| `ActionInMenu` | Mapping: Action ↔ Menu (action nào thuộc menu nào) |
| `Permission` | Mapping: Role ↔ Menu ↔ Action (phân quyền thực tế) |

### Nhóm Blog

| Bảng | Mô tả |
|------|-------|
| `BlogPost` | Bài viết: Title, Slug, Content, ThumbnailUrl, StatusId, LayoutId |
| `BlogPostStatus` | Draft, Published, Archived… |
| `BlogPostCategory` | Danh mục bài viết (many-to-many) |
| `BlogLayout` | Layout template cho bài viết |
| `BlogPostLayout` | Mapping: BlogPost ↔ Layout |
| `BlogPostTag` | Mapping: BlogPost ↔ Tag |
| `BlogPostComment` | Bình luận bài viết |

### Nhóm Hệ thống

| Bảng | Mô tả |
|------|-------|
| `SystemConfig` | Cấu hình key-value động |
| `Notification` / `NotificationCategory` / `NotificationType` | Hệ thống thông báo đa tầng |
| `Tag` / `TagType` | Nhãn đa loại (blog tag, search tag…) |
| `FileUpload` / `FolderUpload` | Quản lý file upload trên Cloudinary |
| `AuditLog` | Lịch sử thay đổi dữ liệu (ai, khi nào, thay đổi gì) |
| `ActivityLog` | Log HTTP request |
| `Province` / `Ward` | Dữ liệu tỉnh thành, phường xã Việt Nam |
| `PaymentTransaction` / `PaymentStatus` / `PaymentMethod` | Quản lý giao dịch thanh toán |
| `Feedback` | Phản hồi từ người dùng |

---

*Tài liệu được tạo tự động từ source code — Backend API Clean Architecture*
