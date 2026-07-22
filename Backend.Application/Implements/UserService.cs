using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using Backend.Application.Common;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Options;
using Backend.Application.DTOs.Emails;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Application.Validators.Users;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Backend.Application.Implements;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly ILogger<UserService> _logger;
    private readonly IStorageService _storageService;
    private readonly IUserVerificationTokenRepository _userVerificationTokenRepository;
    private readonly HostSettings _hostSettings;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IEmailService<GoogleMailRequest> _emailService;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ISystemConfigRepository _systemConfigRepository;

    public UserService(IUserRepository userRepository, IUserRoleRepository userRoleRepository, IMenuRepository menuRepository, IPermissionRepository permissionRepository, ILoggerFactory loggerFactory, IStorageService storageService, IUserVerificationTokenRepository userVerificationTokenRepository, IOptions<HostSettings> hostSettings, IEmailTemplateService emailTemplateService, IEmailService<GoogleMailRequest> emailService, IHttpContextAccessor httpContextAccessor, IUserSessionRepository userSessionRepository, ISystemConfigRepository systemConfigRepository)
    {
        _userRepository = userRepository;
        _userRoleRepository = userRoleRepository;
        _menuRepository = menuRepository;
        _permissionRepository = permissionRepository;
        _logger = loggerFactory.CreateLogger<UserService>();
        _storageService = storageService;
        _userVerificationTokenRepository = userVerificationTokenRepository;
        _hostSettings = hostSettings.Value;
        _emailTemplateService = emailTemplateService;
        _emailService = emailService;
        _httpContextAccessor = httpContextAccessor;
        _userSessionRepository = userSessionRepository;
        _systemConfigRepository = systemConfigRepository;
    }

    public async Task<ApiResponse> CreateAsync(CreateUserDto obj)
    {
        // Bắt buộc chọn ít nhất một vai trò để tài khoản có quyền sử dụng.
        if (obj.Roles == null || obj.Roles.Count == 0)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.RequiredRole),
                ApiCodeConstants.User.RequiredRole
            );
        }

        // Trùng tên đăng nhập (username tách riêng khỏi email).
        var normalizedUsername = obj.Username.Trim().ToLower();
        var duplicateUsername = await _userRepository
            .AnyAsync(x => x.Username.ToLower() == normalizedUsername);
        if (duplicateUsername)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.DuplicatedUsername).Replace("{key}", obj.Username),
                ApiCodeConstants.User.DuplicatedUsername
            );
        }

        var duplicateUser = await _userRepository
            .AnyAsync(x => x.Email.ToLower() == obj.Email.ToLower());
        if (duplicateUser)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.DuplicatedEmail).Replace("{key}", obj.Email),
                ApiCodeConstants.User.DuplicatedEmail
            );
        }

        if (!string.IsNullOrEmpty(obj.PhoneNumber))
        {
            var duplicatePhone = await _userRepository
            .AnyAsync(x => x.PhoneNumber == obj.PhoneNumber);
            if (duplicatePhone)
            {
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.DuplicatedPhoneNumber).Replace("{key}", obj.PhoneNumber),
                    ApiCodeConstants.User.DuplicatedPhoneNumber
                );
            }
        }

        if (!string.IsNullOrEmpty(obj.IdentityNumber))
        {
            var duplicateIdentityNumber = await _userRepository
                .AnyAsync(x => x.IdentityNumber == obj.IdentityNumber);
            if (duplicateIdentityNumber)
            {
                return ApiResponse.UnprocessableEntity(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.User.DuplicatedIdentityNumber).Replace("{key}", obj.IdentityNumber),
                    ApiCodeConstants.User.DuplicatedIdentityNumber
                );
            }
        }

        var model = obj.ToEntity();

        // Hệ thống tự sinh mật khẩu tạm; hash để lưu. Mật khẩu gốc chỉ để gửi email bàn giao.
        var tempPassword = RandomHelper.GeneratePassword(12);
        model.PasswordHash = PasswordHelper.HashPassword(tempPassword);

        await _userRepository.BeginTransactionAsync();
        try
        {

            await _userRepository.CreateAsync(model);
            await _userRepository.SaveChangesAsync();

            await _userRoleRepository.CreateListAsync(obj.Roles.Select(roleId => new UserRole
            {
                RoleId = roleId,
                UserId = model.Id,
                CreatedBy = obj.CreatedBy,
                CreatedDate = DateTime.Now
            }));

            await _userRepository.SaveChangesAsync();
            await _userRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }

        // Bàn giao tài khoản qua email. Lỗi gửi email không làm hỏng việc tạo tài khoản.
        await SendAccountCredentialsEmailAsync(model, tempPassword);

        return ApiResponse.Created(model.Id);
    }

    /// <summary>
    /// Lấy tên hệ thống + logo từ SystemConfig (có giá trị mặc định nếu chưa cấu hình) để dựng email.
    /// </summary>
    private async Task<(string SystemName, string LogoUrl)> GetSystemBrandAsync()
    {
        var systemName = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.SystemName);
        var logoUrl = await _systemConfigRepository.GetValueByKey(SystemConfigConstants.Keys.SystemLogoUrl);

        if (string.IsNullOrWhiteSpace(systemName))
            systemName = SystemConfigConstants.Defaults.SystemName;
        if (string.IsNullOrWhiteSpace(logoUrl))
            logoUrl = SystemConfigConstants.Defaults.SystemLogoUrl;

        return (systemName, logoUrl);
    }

    /// <summary>
    /// Gửi email bàn giao tài khoản mới (username + mật khẩu tạm) cho người dùng.
    /// </summary>
    private async Task SendAccountCredentialsEmailAsync(User user, string tempPassword)
    {
        try
        {
            var (systemName, logoUrl) = await GetSystemBrandAsync();

            var model = new AccountCredentialsEmailDto
            {
                SystemName = systemName,
                LogoUrl = logoUrl,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Username = user.Username,
                Password = tempPassword,
                LoginLink = _hostSettings.AdminUrl,
                Year = DateTime.Now.Year.ToString()
            };

            var emailBody = await _emailTemplateService
                .GetEmailTemplateAsync(AuthConstants.EmailTemplates.ACCOUNT_CREDENTIALS, model);

            var emailRequest = new GoogleMailRequest
            {
                ToEmails = new List<string> { user.Email },
                Subject = AuthConstants.ACCOUNT_CREDENTIALS_EMAIL_TITLE,
                Body = emailBody,
                CcEmails = new List<string>(),
                BccEmails = new List<string>()
            };

            await _emailService.SendMailAsync(emailRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send account credentials email to {Email}: {Message}", user.Email, ex.Message);
        }
    }

    // ── Import hàng loạt (Excel/CSV) ─────────────────────────────────────────

    private static readonly string[] ImportHeaders =
    {
        "Tên đăng nhập", "Email", "Họ và tên đệm", "Tên", "Số điện thoại", "Giới tính (Nam/Nữ)"
    };

    private static readonly string[] ImportSampleRow =
    {
        "nguyenvana", "vana@example.com", "Nguyễn Văn", "A", "0901234567", "Nam"
    };

    // Độ rộng cột tối thiểu (theo số ký tự) để nội dung dài không bị che khi mở Excel.
    private static readonly int[] ImportColumnWidths = { 22, 30, 22, 12, 16, 18 };

    public Task<(byte[] Content, string ContentType, string FileName)> GenerateImportTemplateAsync(string format)
    {
        format = string.IsNullOrWhiteSpace(format) ? "xlsx" : format.Trim().ToLower();

        if (format == "csv")
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", ImportHeaders));
            sb.AppendLine(string.Join(",", ImportSampleRow));
            // BOM UTF-8 để Excel hiển thị tiếng Việt đúng.
            var bytes = new UTF8Encoding(true).GetBytes(sb.ToString());
            return Task.FromResult((bytes, "text/csv; charset=utf-8", "mau-tao-user.csv"));
        }

        IWorkbook wb = new XSSFWorkbook();
        var sheet = wb.CreateSheet("Users");
        var header = sheet.CreateRow(0);
        for (int i = 0; i < ImportHeaders.Length; i++)
            header.CreateCell(i).SetCellValue(ImportHeaders[i]);
        var example = sheet.CreateRow(1);
        for (int i = 0; i < ImportSampleRow.Length; i++)
            example.CreateCell(i).SetCellValue(ImportSampleRow[i]);

        // Căn độ rộng cột: ưu tiên tự động theo nội dung; nếu môi trường (Linux) thiếu font
        // khiến AutoSizeColumn lỗi thì dùng độ rộng tối thiểu cố định để nội dung không bị che.
        for (int i = 0; i < ImportHeaders.Length; i++)
        {
            try { sheet.AutoSizeColumn(i); }
            catch { /* bỏ qua, dùng width tối thiểu bên dưới */ }

            var minWidth = (i < ImportColumnWidths.Length ? ImportColumnWidths[i] : 18) * 256;
            if (sheet.GetColumnWidth(i) < minWidth)
                sheet.SetColumnWidth(i, minWidth);
        }

        using var ms = new MemoryStream();
        wb.Write(ms);
        // MemoryStream.ToArray hoạt động kể cả khi stream đã bị NPOI đóng.
        var content = ms.ToArray();
        return Task.FromResult((content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "mau-tao-user.xlsx"));
    }

    public async Task<ApiResponse> ParseImportFileAsync(Stream fileStream, string fileName)
    {
        try
        {
            // Sao chép ra MemoryStream để NPOI đọc ổn định (cần seek).
            using var ms = new MemoryStream();
            await fileStream.CopyToAsync(ms);
            ms.Position = 0;

            var ext = Path.GetExtension(fileName ?? string.Empty).ToLower();
            var rows = new List<UserImportRowDto>();

            if (ext == ".csv")
            {
                using var reader = new StreamReader(ms, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                string? line;
                bool first = true;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    if (first) { first = false; continue; } // bỏ dòng tiêu đề
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    rows.Add(MapImportRow(SplitCsvLine(line)));
                }
            }
            else
            {
                IWorkbook wb = WorkbookFactory.Create(ms);
                var sheet = wb.GetSheetAt(0);
                if (sheet != null)
                {
                    for (int r = 1; r <= sheet.LastRowNum; r++) // bỏ dòng tiêu đề (row 0)
                    {
                        var row = sheet.GetRow(r);
                        if (row == null) continue;
                        var cols = new string?[ImportHeaders.Length];
                        for (int c = 0; c < ImportHeaders.Length; c++)
                            cols[c] = GetCellString(row.GetCell(c));
                        if (cols.All(string.IsNullOrWhiteSpace)) continue;
                        rows.Add(MapImportRow(cols));
                    }
                }
            }

            return ApiResponse.Success(rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse import file {FileName}: {Message}", fileName, ex.Message);
            return ApiResponse.BadRequest("Không đọc được file. Vui lòng dùng đúng file mẫu (Excel/CSV).", ApiCodeConstants.Common.InvalidFileFormat);
        }
    }

    private static UserImportRowDto MapImportRow(string?[] c)
    {
        string? Get(int i) => (i < c.Length && !string.IsNullOrWhiteSpace(c[i])) ? c[i]!.Trim() : null;

        // Cột: 0=Tên đăng nhập, 1=Email, 2=Họ, 3=Tên, 4=SĐT, 5=Giới tính
        int? gender = null;
        var g = Get(5)?.ToLower();
        if (g == "nam" || g == "male" || g == "1") gender = 1;
        else if (g == "nữ" || g == "nu" || g == "female" || g == "0") gender = 0;

        return new UserImportRowDto
        {
            Username = Get(0),
            Email = Get(1),
            FirstName = Get(2),
            LastName = Get(3),
            PhoneNumber = Get(4),
            Gender = gender
        };
    }

    private static string? GetCellString(ICell? cell)
    {
        if (cell == null) return null;
        switch (cell.CellType)
        {
            case CellType.String:
                return cell.StringCellValue?.Trim();
            case CellType.Numeric:
                var d = cell.NumericCellValue;
                return (d == Math.Floor(d) && !double.IsInfinity(d))
                    ? ((long)d).ToString()
                    : d.ToString(System.Globalization.CultureInfo.InvariantCulture);
            case CellType.Boolean:
                return cell.BooleanCellValue ? "true" : "false";
            case CellType.Formula:
                try { return cell.StringCellValue?.Trim(); }
                catch { return cell.NumericCellValue.ToString(System.Globalization.CultureInfo.InvariantCulture); }
            default:
                return cell.ToString()?.Trim();
        }
    }

    private static string?[] SplitCsvLine(string line)
    {
        var result = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                else inQuotes = !inQuotes;
            }
            else if (ch == ',' && !inQuotes)
            {
                result.Add(sb.ToString());
                sb.Clear();
            }
            else sb.Append(ch);
        }
        result.Add(sb.ToString());
        return result.Select(x => (string?)x.Trim()).ToArray();
    }

    /// <summary>
    /// Tạo hàng loạt user theo kiểu "toàn bộ hoặc không": validate tất cả các dòng (định dạng,
    /// trùng trong lô, trùng DB, bắt buộc vai trò). Nếu có bất kỳ lỗi nào thì KHÔNG tạo ai và trả
    /// về danh sách lỗi theo từng dòng. Nếu hợp lệ hết thì tạo tất cả trong 1 transaction, mỗi user
    /// được sinh mật khẩu tạm + gửi email bàn giao + buộc đổi mật khẩu lần đầu.
    /// </summary>
    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserDto> objs)
    {
        var list = objs?.ToList() ?? new List<CreateUserDto>();
        if (list.Count == 0)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.RequiredMessage).Replace("{PropertyName}", "Danh sách người dùng"),
                ApiCodeConstants.Common.RequiredMessage);

        var validator = new CreateUserDtoValidator();
        var rowErrors = new Dictionary<int, List<string>>();

        void AddError(int row, string message)
        {
            if (!rowErrors.TryGetValue(row, out var msgs))
            {
                msgs = new List<string>();
                rowErrors[row] = msgs;
            }
            if (!msgs.Contains(message)) msgs.Add(message);
        }

        // 1) Validate định dạng từng dòng (dùng chung luật với tạo đơn lẻ).
        for (int i = 0; i < list.Count; i++)
        {
            var vr = validator.Validate(list[i]);
            foreach (var e in vr.Errors)
                AddError(i + 1, e.ErrorMessage);
        }

        // 2) Trùng trong lô (username/email/phone/CCCD).
        void CheckIntraBatchDuplicate(Func<CreateUserDto, string?> selector, string label)
        {
            var seen = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++)
            {
                var raw = selector(list[i]);
                if (string.IsNullOrWhiteSpace(raw)) continue;
                var key = raw.Trim().ToLower();
                if (seen.TryGetValue(key, out var firstRow))
                    AddError(i + 1, $"{label} '{raw}' bị trùng với dòng {firstRow} trong danh sách.");
                else
                    seen[key] = i + 1;
            }
        }
        CheckIntraBatchDuplicate(x => x.Username, "Tên đăng nhập");
        CheckIntraBatchDuplicate(x => x.Email, "Email");
        CheckIntraBatchDuplicate(x => x.PhoneNumber, "Số điện thoại");
        CheckIntraBatchDuplicate(x => x.IdentityNumber, "CCCD/CMND");

        // 3) Trùng với dữ liệu đã có trong DB (truy vấn gộp cho nhanh).
        var usernames = list.Where(x => !string.IsNullOrWhiteSpace(x.Username)).Select(x => x.Username.Trim().ToLower()).Distinct().ToList();
        var emails = list.Where(x => !string.IsNullOrWhiteSpace(x.Email)).Select(x => x.Email.Trim().ToLower()).Distinct().ToList();
        var phones = list.Where(x => !string.IsNullOrWhiteSpace(x.PhoneNumber)).Select(x => x.PhoneNumber!.Trim()).Distinct().ToList();
        var idents = list.Where(x => !string.IsNullOrWhiteSpace(x.IdentityNumber)).Select(x => x.IdentityNumber!.Trim()).Distinct().ToList();

        var existedUsernames = usernames.Count == 0 ? new List<string>() :
            await _userRepository.FindByCondition(x => !x.IsDeleted && usernames.Contains(x.Username.ToLower())).Select(x => x.Username.ToLower()).ToListAsync();
        var existedEmails = emails.Count == 0 ? new List<string>() :
            await _userRepository.FindByCondition(x => !x.IsDeleted && emails.Contains(x.Email.ToLower())).Select(x => x.Email.ToLower()).ToListAsync();
        var existedPhones = phones.Count == 0 ? new List<string>() :
            await _userRepository.FindByCondition(x => !x.IsDeleted && x.PhoneNumber != null && phones.Contains(x.PhoneNumber)).Select(x => x.PhoneNumber!).ToListAsync();
        var existedIdents = idents.Count == 0 ? new List<string>() :
            await _userRepository.FindByCondition(x => !x.IsDeleted && x.IdentityNumber != null && idents.Contains(x.IdentityNumber)).Select(x => x.IdentityNumber!).ToListAsync();

        for (int i = 0; i < list.Count; i++)
        {
            var row = list[i];
            if (!string.IsNullOrWhiteSpace(row.Username) && existedUsernames.Contains(row.Username.Trim().ToLower()))
                AddError(i + 1, $"Tên đăng nhập '{row.Username}' đã tồn tại.");
            if (!string.IsNullOrWhiteSpace(row.Email) && existedEmails.Contains(row.Email.Trim().ToLower()))
                AddError(i + 1, $"Email '{row.Email}' đã tồn tại.");
            if (!string.IsNullOrWhiteSpace(row.PhoneNumber) && existedPhones.Contains(row.PhoneNumber.Trim()))
                AddError(i + 1, $"Số điện thoại '{row.PhoneNumber}' đã tồn tại.");
            if (!string.IsNullOrWhiteSpace(row.IdentityNumber) && existedIdents.Contains(row.IdentityNumber.Trim()))
                AddError(i + 1, $"CCCD/CMND '{row.IdentityNumber}' đã tồn tại.");
        }

        // Có lỗi -> KHÔNG tạo gì, trả về lỗi theo dòng.
        if (rowErrors.Count > 0)
        {
            var errors = rowErrors
                .OrderBy(x => x.Key)
                .Select(x => new UserBatchRowErrorDto { Row = x.Key, Errors = x.Value })
                .ToList();
            return ApiResponse.Error(errors, "Danh sách có lỗi, vui lòng kiểm tra và sửa lại trước khi tạo.", (int)System.Net.HttpStatusCode.UnprocessableEntity, ApiCodeConstants.Common.UnprocessableEntity);
        }

        // Hợp lệ -> tạo tất cả trong 1 transaction.
        var created = new List<(User User, string Password)>();
        await _userRepository.BeginTransactionAsync();
        try
        {
            foreach (var row in list)
            {
                var model = row.ToEntity();
                var tempPassword = RandomHelper.GeneratePassword(12);
                model.PasswordHash = PasswordHelper.HashPassword(tempPassword);

                await _userRepository.CreateAsync(model);
                await _userRepository.SaveChangesAsync(); // cần Id để gán vai trò

                await _userRoleRepository.CreateListAsync(row.Roles.Select(roleId => new UserRole
                {
                    RoleId = roleId,
                    UserId = model.Id,
                    CreatedBy = row.CreatedBy,
                    CreatedDate = DateTime.Now
                }));

                created.Add((model, tempPassword));
            }

            await _userRepository.SaveChangesAsync();
            await _userRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to bulk create users with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }

        // Gửi email bàn giao cho từng user (best-effort, không làm hỏng kết quả tạo).
        foreach (var item in created)
            await SendAccountCredentialsEmailAsync(item.User, item.Password);

        return ApiResponse.Created(created.Select(x => x.User.Id).ToList());
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _userRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new UserListDto
            {
                Id = x.Id,
                Username = x.Username,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Email = x.Email,
                PhoneNumber = x.PhoneNumber,
                AccessFailedCount = x.AccessFailedCount,
                LockEnabled = x.LockEnabled,
                LockEndDate = x.LockEndDate,
                Gender = x.Gender,
                Name = x.FirstName + " " + x.LastName,
                AvatarId = x.AvatarId,
                AvatarUrl = x.Avatar == null ? null : _storageService.GetOriginalUrl(x.Avatar.FileKey),
                CreatedDate = x.CreatedDate,
                UserStatusId = x.UserStatusId,
                UserStatusName = x.UserStatus.Name,
                IdentityNumber = x.IdentityNumber,
                AddressDetail = x.AddresDetail
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _userRepository.FindByCondition(x => x.Id == id && !x.IsDeleted)
                         .Select(x => new UserDetailDto
                         {
                             Id = x.Id,
                             Username = x.Username,
                             FirstName = x.FirstName,
                             LastName = x.LastName,
                             Email = x.Email,
                             PhoneNumber = x.PhoneNumber,
                             AccessFailedCount = x.AccessFailedCount,
                             LockEnabled = x.LockEnabled,
                             LockEndDate = x.LockEndDate,
                             IdentityNumber = x.IdentityNumber,
                             AddressDetail = x.AddresDetail,
                             UserStatus = new DataItem<int>
                             {
                                 Id = x.UserStatus.Id,
                                 Name = x.UserStatus.Name,
                             },
                             Avatar = x.Avatar == null ? null : new FileUploadDetailDto
                             {
                                 Id = x.Avatar.Id,
                                 FileKey = x.Avatar.FileKey,
                                 FileName = x.Avatar.FileName,
                                 FileSize = x.Avatar.FileSize,
                                 FileType = x.Avatar.FileType,
                                 Url = _storageService.GetOriginalUrl(x.Avatar.FileKey)
                             },
                             Roles = x.UserRoles
                                    .Where(ur => !ur.IsDeleted)
                                    .Select(ur => new DataItem<int>
                                    {
                                        Id = ur.Role.Id,
                                        Name = ur.Role.Name,
                                    }).ToList(),
                             Gender = x.Gender,
                             CreatedDate = x.CreatedDate,
                             DateOfBirth = x.DateOfBirth
                         }).FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetMenuAsync(int userId)
    {
        var data = await _userRepository.GetMenuAsync(userId);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _userRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new UserListDto
            {
                Id = x.Id,
                Username = x.Username,
                FirstName = x.FirstName,
                LastName = x.LastName,
                Email = x.Email,
                PhoneNumber = x.PhoneNumber,
                AccessFailedCount = x.AccessFailedCount,
                LockEnabled = x.LockEnabled,
                LockEndDate = x.LockEndDate,
                Gender = x.Gender,
                Name = x.FirstName + " " + x.LastName,
                AvatarId = x.AvatarId,
                AvatarUrl = x.Avatar == null ? null : _storageService.GetOriginalUrl(x.Avatar.FileKey),
                UserStatusId = x.UserStatusId,
                UserStatusName = x.UserStatus.Name,
                IdentityNumber = x.IdentityNumber,
                AddressDetail = x.AddresDetail,
                CreatedDate = x.CreatedDate,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Username.ToLower().Contains(query.Keyword.ToLower()) ||
                x.FirstName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.LastName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Email.ToLower().Contains(query.Keyword.ToLower()) ||
                x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(query.Keyword.ToLower()) ||
                (x.Name).ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<UserListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(UserDTParameters parameters)
    {
        var data = await _userRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _userRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _userRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateUserDto obj)
    {
        var existData = await _userRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == obj.Id)
            .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        if ((existData.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Locked) || existData.LockEnabled) && obj.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active))
        {
            existData.LockEnabled = false;
            existData.LockEndDate = null;
            existData.AccessFailedCount = 0;
        }
        obj.ToEntity(existData);

        try
        {
            await _userRepository.BeginTransactionAsync();

            await _userRepository.UpdateAsync(existData);

            var oldObjs = await _userRoleRepository
                .FindByCondition(x => x.UserId == obj.Id)
                .Select(x => x.Id)
                .ToListAsync();
            await _userRoleRepository.SoftDeleteListAsync(oldObjs);

            var objs = obj.Roles.Select(x => new UserRole
            {
                RoleId = x,
                UserId = obj.Id,
                CreatedBy = obj.UpdatedBy,
                CreatedDate = DateTime.Now
            });
            await _userRoleRepository.CreateListAsync(objs);

            await _userRepository.SaveChangesAsync();
            await _userRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update user with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateUserDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetProfileAsync(int userId)
    {
        var user = await _userRepository
            .FindByCondition(x => x.Id == userId)
            .Select(x => new UserProfileDto
            {
                Id = x.Id,
                Email = x.Email,
                FirstName = x.FirstName,
                Gender = x.Gender,
                LastName = x.LastName,
                PhoneNumber = x.PhoneNumber,
                IdentityNumber = x.IdentityNumber,
                AddresDetail = x.AddresDetail,
                Username = x.Username,
                UserStatus = new DataItem<int>
                {
                    Id = x.UserStatus.Id,
                    Name = x.UserStatus.Name,
                    Description = x.UserStatus.Color,
                },
                UserRoles = x.UserRoles
                    .Where(xx => !xx.IsDeleted && !xx.Role.IsDeleted)
                    .Select(xx => new DataItem<int>
                    {
                        Id = xx.Role.Id,
                        Name = xx.Role.Name,
                    })
                    .ToList(),
                Avatar = x.Avatar == null ? null : new FileUploadDetailDto
                {
                    Id = x.Avatar.Id,
                    FileKey = x.Avatar.FileKey,
                    FileName = x.Avatar.FileName,
                    FileSize = x.Avatar.FileSize,
                    FileType = x.Avatar.FileType,
                    Url = _storageService.GetOriginalUrl(x.Avatar.FileKey)
                }

            })
            .FirstOrDefaultAsync();

        if (user == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound), ApiCodeConstants.Auth.UserNotFound);

        return ApiResponse.Success(user);
    }

    public async Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordDto obj)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound), ApiCodeConstants.Auth.UserNotFound);

        if (!PasswordHelper.VerifyPassword(obj.OldPassword, user.PasswordHash))
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.WrongOldPassword), ApiCodeConstants.Auth.WrongOldPassword);
        }

        // Kiểm tra định dạng mật khẩu mới bằng regex
        var passwordRegex = @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[!@#$%^&*(),.?""{}|<>]).*$";
        if (!Regex.IsMatch(obj.NewPassword, passwordRegex))
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidNewPassword), ApiCodeConstants.Auth.InvalidNewPassword);
        }

        if (!obj.NewPassword.Equals(obj.ConfirmNewPassword))
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword), ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword);
        }

        user.PasswordHash = PasswordHelper.HashPassword(obj.NewPassword);
        // Đã đổi mật khẩu -> gỡ cờ bắt buộc đổi (áp dụng cho cả đổi lần đầu và sau reset).
        user.MustChangePassword = false;
        user.LastModifiedDate = DateTime.Now;
        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();
        return ApiResponse.Success(user);
    }

    public async Task<ApiResponse> UpdateProfileAsync(int userId, UpdateUserProfileDto obj)
    {
        var user = await _userRepository.GetByIdAsync(userId);

        if (user == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound), ApiCodeConstants.Auth.UserNotFound);
        user.FirstName = obj.FirstName;
        user.LastName = obj.LastName;
        user.PhoneNumber = obj.PhoneNumber;
        user.Gender = obj.Gender;
        user.AddresDetail = obj.AddresDetail;
        user.IdentityNumber = obj.IdentityNumber;
        user.AvatarId = obj.AvatarId;
        user.UpdatedBy = userId;
        user.LastModifiedDate = DateTime.Now;


        await _userRepository.UpdateAsync(user);
        await _userRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> GetPermissionsAsync(int userId)
    {
        var data = await _userRepository.GetPermissionsAsync(userId);

        return ApiResponse.Success(data);
    }

    /// <summary>
    /// Số liệu tổng hợp trên TOÀN BỘ user (không theo trang): tổng số, đang hoạt động,
    /// và số user theo từng vai trò. Frontend tự khớp tên vai trò cho các ô thống kê.
    /// </summary>
    public async Task<ApiResponse> GetStatisticsAsync()
    {
        var users = _userRepository.GetAll().Where(x => !x.IsDeleted);
        var totalUsers = await users.CountAsync();
        var activeUsers = await users.CountAsync(x => x.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active));

        // Lấy các bản ghi user-role (kèm tên vai trò) rồi gom nhóm trong bộ nhớ
        // để tránh các vướng mắc dịch GROUP BY/COUNT(DISTINCT) sang SQL.
        var roleRows = await (from ur in _userRoleRepository.GetAll()
                              where !ur.IsDeleted && !ur.User.IsDeleted && !ur.Role.IsDeleted
                              select new { ur.RoleId, RoleName = ur.Role.Name, ur.UserId })
                              .ToListAsync();

        var roleCounts = roleRows
            .GroupBy(x => new { x.RoleId, x.RoleName })
            .Select(g => new RoleCountDto
            {
                RoleId = g.Key.RoleId,
                RoleName = g.Key.RoleName,
                Count = g.Select(x => x.UserId).Distinct().Count()
            })
            .OrderByDescending(x => x.Count)
            .ToList();

        var result = new UserStatisticsDto
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            RoleCounts = roleCounts
        };

        return ApiResponse.Success(result);
    }

    public async Task<ApiResponse> GetPagedEndUserAsync(SearchQuery query)
    {
        var data = (from u in _userRepository.GetAll()
                    join ur in _userRoleRepository.GetAll() on u.Id equals ur.UserId
                    where !u.IsDeleted && !ur.IsDeleted && ur.RoleId != Lookup.RoleId(LookupCodes.Role.Admin) && u.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active)
                    select new UserListDto
                    {
                        Id = u.Id,
                        Username = u.Username,
                        FirstName = u.FirstName,
                        LastName = u.LastName,
                        Email = u.Email,
                        PhoneNumber = u.PhoneNumber,
                        Name = u.FirstName + " " + u.LastName,
                        AvatarId = u.AvatarId,
                        AvatarUrl = u.Avatar == null ? null : _storageService.GetOriginalUrl(u.Avatar.FileKey),
                        CreatedDate = u.CreatedDate,
                        UserStatusId = u.UserStatusId,
                        UserStatusName = u.UserStatus.Name,
                        IdentityNumber = u.IdentityNumber,
                    });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Username.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Email.ToLower().Contains(query.Keyword.ToLower()) ||
                x.PhoneNumber != null && x.PhoneNumber.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.IdentityNumber != null && x.IdentityNumber.ToLower().Contains(query.Keyword.ToLower())
            );

        }
        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }
        var dataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync();


        var pagedData = new PagingData<UserListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = dataSource,
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };
        return ApiResponse.Success(pagedData);
    }

    public async Task<ApiResponse> Deactivate(int userId)
    {
        var user = await _userRepository
            .GetByIdAsync(userId);
        if (user == null)
            return ApiResponse.NotFound();

        user.UserStatusId = Lookup.UserStatusId(LookupCodes.UserStatus.Deactivated);
        user.LastModifiedDate = DateTime.Now;
        user.UpdatedBy = userId;

        await _userRepository.UpdateAsync(user);

        await _userSessionRepository
            .SoftDeleteAsync(x => x.UserId == userId);

        await _userRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> GetAllAsync(UserSearchQuery query)
    {
        var data = await (from u in _userRepository.GetAll()
                          join ur in _userRoleRepository.GetAll() on u.Id equals ur.UserId
                          where !u.IsDeleted && !ur.IsDeleted && ur.RoleId != Lookup.RoleId(LookupCodes.Role.Admin) && u.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active)
                          select new
                          {
                              Id = u.Id,
                              Username = u.Username,
                              FirstName = u.FirstName,
                              LastName = u.LastName,
                              Email = u.Email,
                              PhoneNumber = u.PhoneNumber,
                              Name = u.FirstName + " " + u.LastName,
                              AvatarId = u.AvatarId,
                              AvatarKey = u.Avatar == null ? null : u.Avatar.FileKey,
                              CreatedDate = u.CreatedDate,
                              UserStatusId = u.UserStatusId,
                              UserStatusName = u.UserStatus.Name,
                          })
                    .GroupBy(x => new
                    {
                        x.Id,
                        x.Username,
                        x.FirstName,
                        x.LastName,
                        x.Email,
                        x.PhoneNumber,
                        x.Name,
                        x.AvatarId,
                        x.AvatarKey,
                        x.CreatedDate,
                        x.UserStatusId,
                        x.UserStatusName
                    })
                    .Select(x => new UserListDto
                    {
                        Id = x.Key.Id,
                        Username = x.Key.Username,
                        FirstName = x.Key.FirstName,
                        LastName = x.Key.LastName,
                        Email = x.Key.Email,
                        PhoneNumber = x.Key.PhoneNumber,
                        Name = x.Key.Name,
                        AvatarId = x.Key.AvatarId,
                        AvatarKey = x.Key.AvatarKey,
                        CreatedDate = x.Key.CreatedDate,
                        UserStatusId = x.Key.UserStatusId,
                        UserStatusName = x.Key.UserStatusName
                    })
                    .ToListAsync();

        foreach (var item in data)
        {
            if (!string.IsNullOrEmpty(item.AvatarKey))
                item.AvatarUrl = _storageService.GetOriginalUrl(item.AvatarKey);
        }

        return ApiResponse.Success(data);

    }
}
