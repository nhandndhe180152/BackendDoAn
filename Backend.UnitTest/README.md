# Backend.UnitTest — Project Rules

> Unit Test project cho CI/CD pipeline. Test độc lập, không cần DB, không cần server chạy.
> Đọc `README.md` ở root solution trước khi làm việc với project này.

---

## Tác dụng

- Kiểm thử **business logic** của `Application` layer (Services)
- Kiểm thử **pure functions** của `Share` layer (Helpers, Extensions)
- Chạy trong **CI/CD pipeline** (GitHub Actions, GitLab CI, Azure DevOps)
- Thu thập **code coverage** báo cáo qua Coverlet → Cobertura format

---

## Tech Stack

| Package | Vai trò |
|---|---|
| `xUnit` | Test framework chính |
| `Moq` | Mock các dependency (Repository, Service) |
| `FluentAssertions` | Assertions dễ đọc hơn `Assert.Equal` |
| `coverlet.collector` | Thu thập code coverage |

---

## Cấu trúc thư mục

```
Backend.UnitTest/
├── Services/                   # Test cho Application/Implements/
│   ├── Auth/
│   │   └── AuthServiceTests.cs
│   ├── User/
│   │   └── UserServiceTests.cs
│   └── Role/
│       └── RoleServiceTests.cs
├── Helpers/                    # Test cho Share/Helpers/
│   └── HelperTests.cs          # Password, Email, Phone, String helpers
├── Extensions/                 # Test cho Share/Extensions/
│   └── ExtensionTests.cs       # DateTime, Enumerable extensions
├── Fixtures/                   # Infrastructure hỗ trợ test
│   └── QueryableMockExtensions.cs  # Mock IQueryable async cho EF Core
├── Common/                     # Shared test utilities
│   ├── MockHelper.cs           # Factory tạo mock objects dùng chung
│   └── TestDataBuilder.cs      # Builder tạo test data chuẩn
└── Backend.UnitTest.csproj
```

**Quy tắc đặt file:**
- Test cho `XxxService` → `Services/Xxx/XxxServiceTests.cs`
- Test cho `XxxHelper` → `Helpers/HelperTests.cs` (nhóm nhiều helper cùng file)
- Test cho `XxxExtensions` → `Extensions/ExtensionTests.cs`
- Mỗi class test dùng riêng 1 `public class XxxTests`

---

## Quy tắc viết test

### Đặt tên — bắt buộc theo pattern:

```
MethodName_Condition_ExpectedResult

Ví dụ:
✅ ForgotPassword_EmailNotFound_ReturnsBadRequest
✅ HashPassword_SameInput_ProducesDifferentHashEachTime
✅ IsValidEmail_EmptyString_ReturnsFalse

❌ Test1
❌ ShouldReturnError
❌ ForgotPasswordTest
```

### Cấu trúc test — Arrange / Act / Assert:

```csharp
[Fact]
[Trait("Service", "Auth")]
[Trait("Method", "ForgotPassword")]
public async Task ForgotPassword_EmailNotFound_ReturnsBadRequest()
{
    // Arrange
    _userRepo.Setup(r => r.FirstOrDefaultAsync(It.IsAny<...>()))
             .ReturnsAsync((User?)null);

    // Act
    var result = await _sut.ForgotPasswordAsync("ghost@example.com");

    // Assert
    result.IsSucceeded.Should().BeFalse();
    result.Status.Should().Be(400);
    result.Code.Should().Be(ApiCodeConstants.Auth.EmailNotFound);
}
```

### `[Trait]` bắt buộc — dùng để filter khi chạy CI:

```csharp
[Trait("Service", "Auth")]   // Nhóm (Service / Helper / Extension)
[Trait("Method", "Login")]   // Method đang test
```

### `[Theory]` + `[InlineData]` cho nhiều input cùng logic:

```csharp
[Theory]
[InlineData("valid@email.com",  true)]
[InlineData("notanemail",       false)]
[InlineData("",                 false)]
public void IsValidEmail_VariousInputs_ReturnsExpected(string email, bool expected)
{
    EmailHelper.IsValidEmail(email).Should().Be(expected);
}
```

---

## Setup pattern cho Service Tests

Mỗi test class tạo mocks ở field level, build SUT trong constructor:

```csharp
public class SomeServiceTests
{
    // Fields — không dùng [SetUp] vì xUnit tạo instance mới cho mỗi test
    private readonly Mock<ISomeRepository> _repo = new();

    // SUT được inject đầy đủ
    private readonly SomeService _sut;

    public SomeServiceTests()
    {
        _sut = new SomeService(
            _repo.Object,
            MockHelper.LoggerFactory().Object
        );
    }

    [Fact]
    public async Task SomeMethod_SomeCondition_ExpectedResult()
    {
        // Arrange — setup mock behavior riêng cho test này
        _repo.Setup(r => r.AnyAsync(It.IsAny<Expression<Func<Entity, bool>>>()))
             .ReturnsAsync(true);

        // Act
        var result = await _sut.SomeMethodAsync(dto);

        // Assert
        result.IsSucceeded.Should().BeFalse();
    }
}
```

### Dùng `TestDataBuilder` — không tạo entity thủ công trong test:

```csharp
// ✅ Đúng
var user = TestDataBuilder.DefaultUser();
var user = TestDataBuilder.LockedUser();
var dto  = TestDataBuilder.ValidLoginRequest();

// ❌ Sai — tạo thủ công trong từng test gây lặp code
var user = new User { Id = 1, Username = "test", Email = "..." };
```

### Mock `IQueryable` async — dùng `BuildMock()`:

```csharp
// Khi service dùng FindByCondition().FirstOrDefaultAsync()
_repo.Setup(r => r.FindByCondition(It.IsAny<...>(), It.IsAny<bool>()))
     .Returns(new[] { entity }.AsQueryable().BuildMock());

// Khi query trả về rỗng
_repo.Setup(r => r.FindByCondition(It.IsAny<...>(), It.IsAny<bool>()))
     .Returns(Enumerable.Empty<Entity>().AsQueryable().BuildMock());
```

---

## Chạy test

```bash
# Chạy tất cả test
dotnet test

# Chạy kèm coverage report
dotnet test --collect:"XPlat Code Coverage"

# Filter theo Trait
dotnet test --filter "Trait=Service,Service=Auth"
dotnet test --filter "Trait=Helper"

# Verbose output
dotnet test --logger "console;verbosity=detailed"
```

---

## Coverage targets

| Layer | Target |
|---|---|
| `Share/Helpers` | ≥ 90% — pure functions, dễ test |
| `Share/Extensions` | ≥ 85% |
| `Application/Implements` | ≥ 70% — business logic chính |

Report coverage tự động output ra `TestResults/coverage.cobertura.xml` khi build với MSBuild property `CollectCoverage=true`.

---

## Quy tắc khi thêm Service mới

Khi thêm `XxxService` mới vào `Application/Implements/`, phải:

1. Tạo file `Services/Xxx/XxxServiceTests.cs`
2. Mock tất cả dependency inject vào constructor của `XxxService`
3. Viết test cho **mọi nhánh return** trong mỗi method (đặc biệt các nhánh error)
4. Dùng `TestDataBuilder` cho test data, `MockHelper` cho dependency chung
5. Thêm `[Trait]` cho mọi test method

---

## ⚠️ KHÔNG làm trong Unit Test

- Kết nối DB thật (dùng in-memory hoặc mock)
- Gọi HTTP endpoint thật
- Đọc/ghi file thực sự
- Dùng `Thread.Sleep` hay delay thật
- Để test phụ thuộc vào thứ tự chạy của test khác
- Hardcode magic values — dùng `TestDataBuilder` và constants