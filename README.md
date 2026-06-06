# 🏗️ StockLite Backend API — Clean Architecture Documentation

> **Clean Architecture · .NET 8 · MySQL · Docker · JWT + RBAC**
>
> Tài liệu kỹ thuật cho Backend API của dự án **StockLite** — hệ thống quản lý kho cho SME E-Commerce Businesses.

| 🟢 .NET 8 | 🐬 MySQL 8 | 🔐 JWT + RBAC | 🐳 Docker | 📦 StockLite WMS |
|-----------|------------|---------------|-----------|------------------|

---

## 1. Giới thiệu dự án

**StockLite Backend API** được xây dựng theo kiến trúc **Clean Architecture**, tách rõ các tầng trách nhiệm để dễ bảo trì, kiểm thử và mở rộng. Backend cung cấp RESTful API cho Web Admin Angular, Mobile Flutter và thiết bị IoT cân điện tử trong các nghiệp vụ quản lý kho.

Hệ thống tập trung vào các luồng chính:

- Quản lý người dùng, vai trò và phân quyền.
- Quản lý sản phẩm, Product Variant, SKU và QR Label.
- Quản lý nhập kho, xuất kho, kiểm kê và lịch sử giao dịch tồn kho.
- Tích hợp thiết bị cân IoT qua ESP32 + HX711 để xác minh trọng lượng khi nhận hàng.
- Gợi ý vị trí lưu kho thông minh theo rule-based logic.
- Cảnh báo quá tải nhập kho theo rule-based inbound bottleneck warning.
- Dashboard, báo cáo tồn kho và thông báo.

### Công nghệ chính

| Thành phần | Chi tiết |
|-----------|----------|
| Runtime | .NET 8 (ASP.NET Core Web API) |
| Database | MySQL 8.x |
| ORM | Entity Framework Core 8 — Code-First, Fluent API, Migrations |
| Auth | JWT Bearer + Refresh Token + RBAC |
| Background Jobs | Hangfire + MySQL Storage |
| File Storage | Cloudinary — product images, QR label files |
| Push Notification | Firebase Cloud Messaging (FCM) |
| Email | Gmail SMTP / SMTP Provider |
| Logging | Serilog — console + rolling file logs |
| Caching | ASP.NET Core IMemoryCache |
| API Docs | Swagger / OpenAPI |
| Rate Limiting | ASP.NET Core RateLimiter |
| Deployment | Docker image deployed to Render |
| CI | GitHub Actions |

---

## 2. Tổng quan kiến trúc Clean Architecture

Dự án chia thành nhiều project để đảm bảo dependency chỉ đi từ tầng ngoài vào tầng trong.

```text
Backend.API
   │
   ▼
Backend.Application
   │
   ▼
Backend.Domain
   ▲
   │
Backend.Infrastructure

Backend.Share được dùng chung cho các layer.
```

| Project | Vai trò |
|--------|--------|
| `Backend.API` | Presentation Layer: Controller, Middleware, Swagger, Authentication pipeline |
| `Backend.Application` | Business Logic Layer: Service, DTO, Mapping, Validator |
| `Backend.Domain` | Core Layer: Entity, Enum, Repository Interface, Business abstraction |
| `Backend.Infrastructure` | Infrastructure Layer: DbContext, Repository implementation, External services, Jobs |
| `Backend.Share` | Cross-cutting: ApiResponse, helpers, extensions, constants, common services |

> **Dependency Rule:** `API → Application → Domain`, `Infrastructure → Domain/Application`. Domain không phụ thuộc vào API hoặc Infrastructure.

---

## 3. Các module nghiệp vụ chính

### 3.1 Authentication & Authorization

| Module | Mô tả |
|-------|------|
| Auth | Login, logout, refresh token, đổi mật khẩu, quên mật khẩu |
| User | CRUD người dùng, trạng thái tài khoản, thông tin cá nhân |
| Role | CRUD vai trò |
| Permission | Phân quyền theo Role × Menu × Action |
| UserSession | Lưu JWT session, refresh token, revoke token |

### 3.2 Product & QR Label

| Module | Mô tả |
|-------|------|
| Product | Thông tin sản phẩm chung, không chứa SKU trực tiếp |
| ProductVariant | Biến thể sản phẩm, chứa SKU, đơn vị tính, thuộc tính, min stock level |
| Attribute / AttributeValue | Thuộc tính biến thể, ví dụ màu sắc, kích thước |
| QR Label | Sinh mã QR cho Product Variant / lô nhập hàng để scan khi nhận hàng hoặc kiểm kê |
| Product Image | Upload và quản lý ảnh sản phẩm qua Cloudinary |

### 3.3 Warehouse, Location & Inventory

| Module | Mô tả |
|-------|------|
| Warehouse | Quản lý kho |
| Location | Vị trí lưu trữ trong kho |
| Inventory | Số lượng tồn hiện tại theo ProductVariant, Warehouse, Location |
| InventoryTransaction | Lịch sử nhập, xuất, điều chỉnh, kiểm kê |
| Stock Take | Kiểm kê tồn kho bằng scan QR và cập nhật chênh lệch |

### 3.4 Inbound & Outbound

| Module | Mô tả |
|-------|------|
| Inbound / Purchase Order | Tạo phiếu nhập, chi tiết phiếu nhập, xác nhận nhận hàng |
| Outbound / Sales Order | Tạo phiếu xuất, picking, packing và xác nhận xuất hàng |
| Receiving | Scan QR, kiểm tra thông tin hàng, xác minh cân IoT, confirm nhận hàng |
| Shipping | Scan QR, xác nhận lấy hàng, đóng gói và xuất kho |

### 3.5 IoT Scale Integration

Thiết bị IoT dự kiến dùng:

```text
ESP32 CP2102 + HX711 + load cell + OLED I2C
```

Thiết bị gửi dữ liệu qua REST API:

```text
POST /api/iot/weight-logs
GET  /api/iot/devices/{deviceCode}/commands/pending
POST /api/iot/device-commands/{commandId}/complete
```

| Module | Mô tả |
|-------|------|
| IoTDevice | Quản lý thiết bị cân tại kho hoặc vị trí nhận hàng |
| IoTWeightLog | Lưu log trọng lượng ổn định do ESP32 gửi lên backend |
| IoTDeviceCommand | Backend tạo lệnh như TARE / RESET / CALIBRATE để thiết bị polling |
| Receiving Verification | So sánh trọng lượng / số lượng kỳ vọng trước khi xác nhận nhập kho |

### 3.6 Smart Put-away Location Suggestion

Tính năng gợi ý vị trí lưu kho dựa trên rule-based logic:

- Product Variant cần lưu.
- Kho được chọn.
- Sức chứa còn lại của Location.
- Vị trí ưu tiên.
- Tồn kho hiện tại của cùng Product Variant.
- Trạng thái khả dụng của Location.

Kết quả trả về là danh sách vị trí đề xuất, nhân viên vẫn là người xác nhận cuối cùng.

### 3.7 Rule-Based Inbound Bottleneck Warning

Tính năng cảnh báo nguy cơ quá tải nhập kho dựa trên:

- Số lượng phiếu nhập dự kiến trong 24 giờ.
- Tổng số item cần nhận.
- Năng lực xử lý dự kiến của nhân sự.
- Sức chứa kho / khu nhận hàng.
- Trạng thái tồn đọng của các phiếu chưa xử lý.

Đây là rule engine đơn giản, không phải AI/ML.

---

## 4. Chi tiết các project

### 4.1 Backend.API — Presentation Layer

Project này là điểm vào của hệ thống, chứa HTTP pipeline và Controller.

| Thành phần | Mô tả |
|-----------|------|
| `Controllers/` | API Controller cho từng module |
| `Middlewares/` | Exception handling, token revocation, request logging |
| `Utilities/` | Extension method đăng ký service, CORS, Swagger, Auth, Rate Limit |
| `Program.cs` | Entry point cấu hình app |
| `appsettings.json` | Cấu hình chung dạng placeholder |
| `appsettings.Development.json` | Cấu hình local, không commit nếu có secret thật |

Controller dự kiến:

| Controller | Mô tả |
|-----------|------|
| `AuthController` | Đăng nhập, refresh token, logout |
| `UserController` | Quản lý người dùng |
| `RoleController` | Quản lý vai trò |
| `ProductController` | Quản lý sản phẩm |
| `ProductVariantController` | Quản lý biến thể/SKU |
| `WarehouseController` | Quản lý kho |
| `LocationController` | Quản lý vị trí |
| `InventoryController` | Truy vấn tồn kho |
| `InventoryTransactionController` | Lịch sử giao dịch tồn kho |
| `InboundController` | Phiếu nhập / receiving |
| `OutboundController` | Phiếu xuất / shipping |
| `QrLabelController` | Sinh và tra cứu QR |
| `IoTDeviceController` | Quản lý thiết bị IoT |
| `IoTWeightLogController` | Nhận log cân |
| `IoTDeviceCommandController` | Quản lý lệnh thiết bị |
| `DashboardController` | Dữ liệu dashboard |
| `ReportController` | Báo cáo tồn kho / hoạt động |

### 4.2 Backend.Application — Business Logic Layer

Chứa service, DTO, mapping, validator và logic nghiệp vụ.

| Thành phần | Mô tả |
|-----------|------|
| `Interfaces/` | Interface service |
| `Implements/` | Implement service |
| `DTOs/` | Request/Response DTO |
| `Mappings/` | AutoMapper Profile |
| `Validators/` | FluentValidation |
| `Constants/` | Mã lỗi, message, business constants |
| `DependencyInjection/` | Đăng ký service vào DI |

Service chính:

| Service | Mô tả |
|--------|------|
| `AuthService` | Xác thực, JWT, refresh token |
| `UserService` | Người dùng |
| `RoleService` | Vai trò |
| `ProductService` | Sản phẩm |
| `ProductVariantService` | Biến thể/SKU |
| `WarehouseService` | Kho |
| `LocationService` | Vị trí lưu kho |
| `InventoryService` | Tồn kho hiện tại |
| `InventoryTransactionService` | Nhập/xuất/điều chỉnh tồn kho |
| `InboundService` | Phiếu nhập và nhận hàng |
| `OutboundService` | Phiếu xuất và giao hàng |
| `QrLabelService` | QR label |
| `IoTDeviceService` | Thiết bị IoT |
| `IoTWeightLogService` | Log cân |
| `IoTDeviceCommandService` | Lệnh thiết bị |
| `PutAwaySuggestionService` | Gợi ý vị trí lưu hàng |
| `InboundBottleneckWarningService` | Cảnh báo quá tải nhập kho |
| `DashboardService` | Dashboard |
| `ReportService` | Báo cáo |

### 4.3 Backend.Domain — Core Layer

Chứa entity, enum, abstraction và repository interface. Domain không phụ thuộc vào project khác.

| Entity | Mô tả |
|-------|------|
| `User` | Người dùng |
| `Role` | Vai trò |
| `Permission` | Phân quyền |
| `Product` | Sản phẩm |
| `ProductVariant` | Biến thể/SKU |
| `Warehouse` | Kho |
| `Location` | Vị trí |
| `Inventory` | Tồn kho |
| `InventoryTransaction` | Lịch sử giao dịch tồn kho |
| `Inbound` / `InboundDetail` | Phiếu nhập |
| `Outbound` / `OutboundDetail` | Phiếu xuất |
| `QrLabel` | QR label |
| `IoTDevice` | Thiết bị IoT |
| `IoTWeightLog` | Log cân |
| `IoTDeviceCommand` | Lệnh thiết bị |
| `AuditLog` | Audit trail |
| `ActivityLog` | Log request/activity |

### 4.4 Backend.Infrastructure — Infrastructure Layer

Implement repository, DbContext, external services và background jobs.

| Thành phần | Mô tả |
|-----------|------|
| `Persistence/` | DbContext, configurations, migrations, seed data |
| `Repositories/` | Implement repository |
| `Services/` | Cloudinary, Email, Firebase, Token provider |
| `Interceptors/` | Audit interceptor |
| `Jobs/` | Hangfire recurring jobs |
| `DependencyInjection/` | Đăng ký Infrastructure service |

### 4.5 Backend.Share — Cross-cutting Concerns

Thư viện dùng chung giữa các layer.

| Thành phần | Mô tả |
|-----------|------|
| `Entities/` | `ApiResponse<T>`, paging model, file upload result |
| `Extensions/` | Extension methods cho string, datetime, query, http context |
| `Helpers/` | Helper cho password, email, file, random, string |
| `Services/` | Cache service, serialize service |
| `Constants/` | Shared constants |
| `Attributes/` | Custom attributes |

---

## 5. Luồng request điển hình

```text
HTTP Request
   │
   ▼
Rate Limiter
   │
   ▼
Exception Handling Middleware
   │
   ▼
Authentication Middleware
   │
   ▼
Authorization / RBAC
   │
   ▼
Controller
   │
   ▼
Application Service
   │
   ▼
Repository / UnitOfWork
   │
   ▼
EF Core / MySQL
   │
   ▼
ApiResponse<T>
```

---

## 6. Cấu hình hệ thống

### 6.1 ConnectionStrings

Không commit connection string thật lên GitHub. Dùng placeholder trong `appsettings.json` và inject giá trị thật bằng `.env`, Docker Compose hoặc secret của CI/CD.

```json
{
  "ConnectionStrings": {
    "DefaultConnectionString": "Server=<MYSQL_HOST>;Port=3306;Database=<DB_NAME>;User=<DB_USER>;Password=<DB_PASSWORD>;SslMode=Required;AllowPublicKeyRetrieval=True;"
  }
}
```

### 6.2 JwtSettings

```json
{
  "JwtSettings": {
    "SecretKey": "<32+ random characters>",
    "Issuer": "StockLiteApi",
    "Audience": "StockLiteClient",
    "ExpireTime": 1,
    "RefreshTokenTtl": 7
  }
}
```

### 6.3 CloudinarySettings

```json
{
  "CloudinarySettings": {
    "CloudName": "<cloud_name>",
    "ApiKey": "<api_key>",
    "ApiSecret": "<api_secret>"
  }
}
```

---

## 7. Docker & Environment Variables

### 7.1 Nguyên tắc

Docker image chỉ nên chứa application binaries. Các giá trị môi trường như connection string, JWT secret, SMTP password, Cloudinary secret và Firebase config phải được inject khi container chạy.

Không commit các file chứa secret thật:

```text
.env
appsettings.Development.json
appsettings.Production.json
firebase-service.json
docker-compose.override.yml
```

### 7.2 Ví dụ docker-compose.yml

```yaml
services:
  stocklite-api:
    image: stocklite-api:latest
    build:
      context: .
      dockerfile: Backend.API/Dockerfile
    ports:
      - "8080:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Production
      ConnectionStrings__DefaultConnectionString: ${DB_CONNECTION_STRING}
      JwtSettings__SecretKey: ${JWT_SECRET}
      CloudinarySettings__CloudName: ${CLOUDINARY_CLOUD_NAME}
      CloudinarySettings__ApiKey: ${CLOUDINARY_API_KEY}
      CloudinarySettings__ApiSecret: ${CLOUDINARY_API_SECRET}
      SmtpSettings__Username: ${SMTP_USERNAME}
      SmtpSettings__Password: ${SMTP_PASSWORD}
```

### 7.3 Ví dụ .env.example

```env
DB_CONNECTION_STRING=
JWT_SECRET=
CLOUDINARY_CLOUD_NAME=
CLOUDINARY_API_KEY=
CLOUDINARY_API_SECRET=
SMTP_USERNAME=
SMTP_PASSWORD=
FIREBASE_CREDENTIAL_PATH=
```

---

## 8. Hướng dẫn chạy dự án local

### 8.1 Yêu cầu môi trường

| Thành phần | Yêu cầu |
|-----------|---------|
| .NET SDK | 8.0 trở lên |
| MySQL | 8.x |
| Docker | Docker Desktop hoặc Docker Engine |
| Git | Phiên bản mới |
| IDE | Visual Studio 2022 / Visual Studio Code |
| Cloudinary | Tài khoản Cloudinary nếu cần upload ảnh |
| Firebase | Firebase project nếu cần push notification |

### 8.2 Chạy bằng .NET CLI

```bash
dotnet restore
dotnet build

dotnet ef database update   --project Backend.Infrastructure   --startup-project Backend.API

dotnet run --project Backend.API
```

Sau khi chạy, mở:

```text
https://localhost:<port>/swagger
```

### 8.3 Chạy bằng Docker Compose

```bash
cp .env.example .env
# cập nhật giá trị trong .env

docker compose up --build
```

---

## 9. Endpoint đặc biệt

| Endpoint | Mô tả |
|---------|------|
| `/swagger` | Swagger UI |
| `/jobs` | Hangfire Dashboard |
| `/health/live` | Liveness check |
| `/health/ready` | Readiness check |
| `/api/auth/login` | Đăng nhập |
| `/api/inbounds` | Quản lý nhập kho |
| `/api/outbounds` | Quản lý xuất kho |
| `/api/inventory` | Truy vấn tồn kho |
| `/api/inventory-transactions` | Lịch sử giao dịch tồn kho |
| `/api/iot/weight-logs` | Nhận log cân IoT |
| `/api/put-away-suggestions` | Gợi ý vị trí lưu kho |
| `/api/inbound-bottleneck-warnings` | Cảnh báo quá tải nhập kho |

---

## 10. Bảo mật & phân quyền

### 10.1 JWT Authentication Flow

```text
POST /api/auth/login
   → Validate credentials
   → Generate access token + refresh token
   → Store user session
   → Return token pair

Next requests:
   Authorization: Bearer <access_token>
   → Validate JWT
   → Check permission
   → Execute API action

POST /api/auth/refresh-token
   → Validate refresh token
   → Revoke old session
   → Issue new token pair
```

### 10.2 RBAC

Hệ thống dùng phân quyền theo mô hình:

```text
User → UserRole → Role → Permission → Menu + Action
```

Ví dụ action:

```text
CREATE, READ, UPDATE, DELETE, EXPORT, APPROVE, CONFIRM
```

Ví dụ menu/module:

```text
DASHBOARD, PRODUCT, PRODUCT_VARIANT, INBOUND, OUTBOUND, INVENTORY, IOT_DEVICE, REPORT
```

---

## 11. Logging & Audit

StockLite dùng Serilog và AuditLog để hỗ trợ debug và truy vết nghiệp vụ.

| Thành phần | Mô tả |
|-----------|------|
| Info Log | Ghi thông tin request, xử lý nghiệp vụ thông thường |
| Error Log | Ghi exception và lỗi hệ thống |
| ActivityLog | Ghi request activity |
| AuditLog | Ghi thay đổi dữ liệu quan trọng như Inventory, Inbound, Outbound, ProductVariant |

---

## 12. Cấu trúc database chính

| Nhóm | Bảng / Entity |
|-----|---------------|
| Auth & RBAC | User, Role, UserRole, Permission, UserSession |
| Product | Product, ProductVariant, Attribute, AttributeValue, ProductImage |
| Warehouse | Warehouse, Location |
| Inventory | Inventory, InventoryTransaction, StockTake |
| Inbound | Inbound, InboundDetail |
| Outbound | Outbound, OutboundDetail |
| QR | QrLabel |
| IoT | IoTDevice, IoTWeightLog, IoTDeviceCommand |
| System | Notification, AuditLog, ActivityLog, SystemConfig |

---

## 13. CI/CD

GitHub Actions chạy build và test cho Pull Request vào `develop` và `main`.

Luồng branch khuyến nghị:

```text
feature/* → develop → main
```

- `develop`: nhánh tích hợp trong quá trình phát triển.
- `main`: nhánh ổn định để demo/release.
- CI chạy build/test trước khi merge.
- Docker image được build và deploy lên Render theo cấu hình của nhóm.

---

## 14. Ghi chú bảo mật khi public repo

Trước khi public repo, kiểm tra chắc chắn không commit:

```text
.env
appsettings.Development.json
appsettings.Production.json
firebase-service.json
google-services.json
*.pfx
*.pem
*.key
*.jks
database backup thật
connection string thật
JWT secret thật
SMTP password thật
Cloudinary API secret thật
```

Nên dùng:

```text
.env.example
appsettings.example.json
```

để mô tả cấu hình cần thiết mà không lộ thông tin thật.

---

*Tài liệu được cập nhật cho dự án StockLite Backend API.*
