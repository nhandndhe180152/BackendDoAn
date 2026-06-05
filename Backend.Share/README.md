# Backend.Share — Project Rules

> Shared Kernel — Thư viện dùng chung cho toàn bộ solution. Không phụ thuộc vào project nào khác.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Cung cấp **types dùng chung** mà nhiều layer cần: `ApiResponse`, `SearchQuery`, `PagingData`
- Cung cấp **extension methods** tiện ích: DateTime, String, HttpContext, LINQ
- Cung cấp **helper classes**: File, Email, Phone, Password, Cron, Random, String
- Cung cấp **shared services** nhỏ: Cache, Serialization, Image processing, Scheduled jobs
- Cung cấp **constants và enums** chia sẻ giữa các layer
- **Không** chứa business logic, không biết về Entity hay DB

---

## Cấu trúc thư mục

```
Backend.Share/
├── Entities/               # Shared types (không phải DB entities)
│   ├── ApiResponse.cs          # ⭐ Response wrapper chuẩn của toàn hệ thống
│   ├── SearchQuery.cs          # Query params cho paging cơ bản
│   ├── PagingData.cs           # Kết quả paging
│   ├── DataTableModel.cs       # Paging model kiểu DataTable
│   ├── DataItem.cs             # Generic key-value item
│   ├── ApexChartData.cs        # Data cho chart
│   ├── FolderUploadResult.cs   # Kết quả upload folder
│   ├── DetailStatusDto.cs
│   ├── Select2Parameters.cs    # Params cho Select2 dropdown
│   ├── ProvinceApiResponse.cs  # Response từ external province API
│   └── WardApiResponse.cs      # Response từ external ward API
├── Extensions/             # Extension methods
│   ├── DateTimeExtensions.cs   # DateTime formatting, conversion
│   ├── StringExtensions.cs     # String utilities (slug, trim, normalize)
│   ├── HttpContextExtensions.cs# Lấy IP, user agent từ HttpContext
│   ├── HttpClientExtensions.cs # HTTP call helpers
│   ├── EnumerableExtensions.cs # IEnumerable utilities
│   ├── LinqExtensions.cs       # LINQ helpers
│   └── ImageProcessorExtentions.cs
├── Helpers/                # Static helper classes
│   ├── PasswordHelper.cs       # Hash & verify password (BCrypt)
│   ├── RandomHelper.cs         # Generate OTP, random string
│   ├── EmailHelper.cs          # Validate email format
│   ├── PhoneHelper.cs          # Validate & format phone number
│   ├── FileHelper.cs           # File type detection, size formatting
│   ├── StringHelper.cs         # Slug generation, normalize
│   └── CronHelper.cs           # Parse & describe cron expressions
├── Services/               # Shared service interfaces & implementations
│   ├── ICacheService.cs        # Interface cache
│   ├── MemoryCacheService.cs   # In-memory cache implementation
│   ├── IImageProcessor.cs      # Interface xử lý ảnh
│   ├── MagickImageProcessor.cs # ImageMagick implementation
│   ├── ISerializeService.cs    # Interface serialize
│   ├── SerializeService.cs     # JSON serialize/deserialize
│   ├── IScheduledJobService.cs # Interface background jobs
│   └── ScheduledJobService.cs
├── Constants/
│   ├── ClaimNames.cs           # JWT claim names constants
│   └── SQLParams.cs            # SQL parameter name constants
├── Enums/
│   └── CommonEnum.cs           # Enums dùng chung (không domain-specific)
├── Attributes/
│   ├── SensitiveDataAttribute.cs    # Đánh dấu property nhạy cảm (không log)
│   └── SortTypeValidateAttribute.cs # Validate sort direction
└── Backend.Share.csproj
```

---

## ApiResponse — Type quan trọng nhất

**Mọi service method đều trả `ApiResponse`.** Đây là contract giữa các layer.

```csharp
// Cấu trúc ApiResponse
{
    "isSucceeded": true/false,
    "status": 200,          // HTTP status code
    "code": "CMN_200",      // Internal error code
    "message": "Success",
    "resources": { ... },   // Data khi thành công
    "errors": null          // Error details khi thất bại
}
```

**Factory methods — chỉ dùng những method này:**

```csharp
ApiResponse.Success<T>(data, message)       // 200 có data
ApiResponse.Success(message)                 // 200 không data
ApiResponse.Created<T>(data, message)        // 201
ApiResponse.NotFound(message, code)          // 404
ApiResponse.BadRequest(message, code)        // 400
ApiResponse.BadRequest<T>(errors, message, code) // 400 với validation errors
ApiResponse.Unauthorized(message, code)      // 401
ApiResponse.Forbidden(message, code)         // 403
ApiResponse.UnprocessableEntity(errors, msg, code) // 422 — validation form errors
ApiResponse.InternalServerError()            // 500
```

---

## SearchQuery — Paging cơ bản

```csharp
// Share/Entities/SearchQuery.cs
public class SearchQuery
{
    public string? Keyword { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string? SortBy { get; set; }
    public string? SortType { get; set; }  // "asc" hoặc "desc"
}
```

---

## Helpers hay dùng

```csharp
// Password
var hashed = PasswordHelper.HashPassword("rawPassword");
bool isValid = PasswordHelper.VerifyPassword("rawPassword", hashed);

// OTP & random
string otp = RandomHelper.GenerateOtpCode();           // 6 chữ số
string token = RandomHelper.GenerateRandomString(50);  // random string

// File
string extension = FileHelper.GetExtension(fileName);
bool isImage = FileHelper.IsImage(fileName);
string sizeStr = FileHelper.FormatSize(bytes);  // "1.5 MB"

// Phone
bool isValid = PhoneHelper.IsValidVietnamesePhone("0912345678");

// Email
bool isValid = EmailHelper.IsValidEmail("test@example.com");

// String — Slug
string slug = StringHelper.GenerateSlug("Tiêu đề bài viết"); // "tieu-de-bai-viet"
```

---

## Extension Methods hay dùng

```csharp
// DateTime
DateTime.Now.ToVietnamTime()         // Convert sang UTC+7
date.ToShortDateVN()                 // "15/04/2026"
date.IsExpired()                     // bool

// String
"  hello  ".TrimAll()                // "hello"
"Hello World".ToSlug()               // "hello-world"
str.IsNullOrEmpty()                  // shorthand

// LINQ
list.DistinctBy(x => x.Id)
list.IsNullOrEmpty()

// HttpContext
httpContext.GetClientIpAddress()
httpContext.GetUserAgent()
```

---

## Quy tắc khi làm việc với Share

1. **Chỉ thêm vào Share** những gì thực sự dùng ở ≥ 2 layer khác nhau
2. **Không thêm** business logic hay domain-specific code vào đây
3. **Không reference** bất kỳ project nào trong solution từ Share
4. Helper method phải là **static, stateless** — không có side effects
5. Extension method đặt trong `Extensions/`, helper class đặt trong `Helpers/`
6. Khi thêm constant mới: `ClaimNames` cho JWT claims, tạo file mới nếu nhóm constant đủ lớn
