# Backend.Application — Project Rules

> Application Layer (Use Cases). Điều phối business logic, không chứa domain rules hay infrastructure concerns.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Chứa toàn bộ **business logic** của ứng dụng
- Định nghĩa **interface** cho service và repository (contract)
- Xử lý **DTO mapping**, **validation**, **authorization** ở tầng use-case
- Gửi email, xử lý file upload, gọi external services (qua interface)
- **Không** biết về HTTP, không biết về database implementation

---

## Cấu trúc thư mục

```
Backend.Application/
├── Interfaces/             # Contracts — service interfaces
│   ├── IServiceBase.cs     # Generic interface: GetById, Create, Update, Delete, GetPaged
│   ├── IAuthService.cs
│   ├── IUserService.cs
│   └── ...                 # Mỗi domain một interface
├── Implements/             # Implementations của Interfaces
│   ├── AuthService.cs
│   ├── UserService.cs
│   └── ...
├── DTOs/                   # Data Transfer Objects
│   ├── Users/
│   │   ├── CreateUserDto.cs
│   │   ├── UpdateUserDto.cs
│   │   ├── UserDto.cs
│   │   └── UserSearchQuery.cs
│   └── ...                 # Phân loại theo entity
├── Mappings/               # AutoMapper profiles
│   └── UserMappingProfile.cs
├── Validators/             # FluentValidation validators
│   └── CreateUserDtoValidator.cs
├── Constants/              # ApiCodeConstants, ErrorMessagesConstants, NotificationConstants
├── DependencyInjection/    # Đăng ký DI
│   ├── Options/            # Strongly-typed config classes (HostSettings, ...)
│   └── Extentions/         # ConfigureServices.cs — đăng ký tất cả service
├── EmailTemplates/         # HTML email templates
├── StaticFiles/            # Static file resources
└── Backend.Application.csproj
```

---

## Quy tắc Service

### Interface (Application/Interfaces/)

```csharp
// Interface kế thừa IServiceBase cho CRUD chuẩn
public interface IUserService : IServiceBase<int, CreateUserDto, UpdateUserDto, UserDTParameters>
{
    // Thêm method đặc thù không có trong IServiceBase
    Task<ApiResponse> GetProfileAsync(int userId);
    Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordDto dto);
    Task<ApiResponse> GetMenuAsync(int userId);
}
```

### Implementation (Application/Implements/)

```csharp
public class UserService : IUserService
{
    // Inject repository interfaces — không inject DbContext
    private readonly IUserRepository _userRepository;
    private readonly IMapper _mapper;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, IMapper mapper, ILoggerFactory loggerFactory)
    {
        _userRepository = userRepository;
        _mapper = mapper;
        _logger = loggerFactory.CreateLogger<UserService>();
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var entity = await _userRepository.FirstOrDefaultAsync(x => x.Id == id);
        if (entity == null)
            return ApiResponse.NotFound(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.NotFound),
                ApiCodeConstants.Common.NotFound);

        var dto = _mapper.Map<UserDto>(entity);
        return ApiResponse.Success(data: dto);
    }

    public async Task<ApiResponse> CreateAsync(CreateUserDto obj)
    {
        // Kiểm tra duplicate nếu cần
        var existed = await _userRepository.AnyAsync(x => x.Email == obj.Email);
        if (existed)
            return ApiResponse.BadRequest(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.EmailExisted),
                ApiCodeConstants.User.EmailExisted);

        var entity = _mapper.Map<User>(obj);
        await _userRepository.CreateAsync(entity);
        await _userRepository.SaveChangesAsync();

        return ApiResponse.Created(message: "Tạo user thành công");
    }
}
```

### Quy tắc bắt buộc trong Service

- Luôn trả `ApiResponse` — không throw exception ra ngoài (ngoại trừ lỗi thật sự không xử lý được)
- Gọi `SaveChangesAsync()` trong Service, **không** trong Repository
- Sử dụng `IMapper` để map — không map thủ công
- Log exception trước khi trả error response: `_logger.LogError(ex, "...")`
- Validate business rules ở đây, validate format/required ở FluentValidation

---

## Quy tắc DTO

```csharp
// Application/DTOs/{EntityName}/

// Request DTOs
public class CreateUserDto
{
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    // Audit fields được set bởi Controller hoặc Service
    public int CreatedBy { get; set; }
}

public class UpdateUserDto
{
    public int Id { get; set; }  // Luôn có Id trong UpdateDto
    public string FirstName { get; set; } = null!;
    public int UpdatedBy { get; set; }
}

// Response DTO (View Model)
public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = null!;
    public string Email { get; set; } = null!;
    // Không bao giờ include: PasswordHash, sensitive tokens
}
```

**Quy tắc DTO:**
- `CreateXxxDto` → POST body (không có Id)
- `UpdateXxxDto` → PUT body (bắt buộc có Id)
- `XxxDto` → Response / view model
- `XxxSearchQuery` → Query params, thường extends `SearchQuery` hoặc `AdvancedSearchQuery`
- **Không bao giờ** include `PasswordHash`, token secrets, hoặc thông tin nhạy cảm trong response DTO

---

## Quy tắc Mapping (Mappings/)

Dự án **không sử dụng AutoMapper**. Tất cả việc chuyển đổi dữ liệu giữa Entity và DTO phải được thực hiện thủ công thông qua các **Extension Methods** để đảm bảo hiệu năng cao nhất và dễ dàng debug.

**Ví dụ cấu trúc một file Mapping:**

```csharp
public static class UserMapping
{
    // Entity -> List DTO
    public static UserListDto ToListDto(this User entity)
    {
        return new UserListDto
        {
            Id = entity.Id,
            Email = entity.Email,
            FullName = entity.FullName,
            StatusName = entity.UserStatus?.Name
        };
    }

    // Create DTO -> Entity
    public static User ToEntity(this CreateUserDto dto)
    {
        return new User
        {
            Email = dto.Email,
            FullName = dto.FullName,
            // PasswordHash sẽ được xử lý riêng tại Service
        };
    }
}
```

---

## Quy tắc FluentValidation (Application/Validators/)

```csharp
public class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email không được để trống")
            .EmailAddress().WithMessage("Email không đúng định dạng");

        RuleFor(x => x.PasswordHash)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự");
    }
}
```

---

## Thêm Service mới

1. Tạo `Interfaces/IXxxService.cs` kế thừa `IServiceBase<TKey, CreateDto, UpdateDto, DTParam>`
2. Tạo `DTOs/Xxx/` với đầy đủ Create, Update, View DTOs
3. Tạo `Mappings/XxxMappingProfile.cs`
4. Tạo `Validators/CreateXxxDtoValidator.cs`
5. Tạo `Implements/XxxService.cs`
6. Đăng ký trong `DependencyInjection/Extentions/ConfigureServices.cs`:
   ```csharp
   services.AddScoped<IXxxService, XxxService>();
   ```

---

## Constants quan trọng

| File | Nội dung |
|------|----------|
| `Constants/ApiCodeConstants.cs` | Error codes theo domain (Auth, User, ...) |
| `Constants/ErrorMessagesConstants.cs` | Map code → message string |
| `Constants/NotificationConstants.cs` | Constants cho notification system |
| `Constants/AuthConstants.cs` | Auth limits (max login attempts, token TTL, ...) |
