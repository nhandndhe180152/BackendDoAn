using System;
using Backend.Application.Common;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Options;
using Backend.Application.DTOs.Auths;
using Backend.Application.DTOs.Emails;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static Backend.Application.Constants.ApiCodeConstants;

namespace Backend.Application.Implements;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IUserSessionRepository _userSessionRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly ITokenProviderService _tokenProviderService;
    private readonly IStorageService _storageService;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IUserVerificationTokenRepository _userVerificationTokenRepository;
    private readonly IEmailService<GoogleMailRequest> _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly HostSettings _hostSettings;
    private readonly IEmailTemplateService _emailTemplateService;
    private readonly IMenuRepository _menuRepository;
    private readonly IRoleRepository _roleRepository;
    private readonly ILogger<AuthService> _logger;
    private readonly IFileUploadRepository _fileUploadRepository;
    private readonly ISystemConfigRepository _systemConfigRepository;

    public AuthService(IUserRepository userRepository, IUserSessionRepository userSessionRepository, ITokenProviderService tokenProviderService, IUserRoleRepository userRoleRepository, IStorageService storageService, IPermissionRepository permissionRepository, IEmailService<GoogleMailRequest> emailService, IUserVerificationTokenRepository userVerificationTokenRepository, IHttpContextAccessor httpContextAccessor, IOptions<HostSettings> hostSettings, IEmailTemplateService emailTemplateService, IMenuRepository menuRepository, IRoleRepository roleRepository, ILoggerFactory loggerFactory, IFileUploadRepository fileUploadRepository, ISystemConfigRepository systemConfigRepository)
    {
        _userRepository = userRepository;
        _userSessionRepository = userSessionRepository;
        _tokenProviderService = tokenProviderService;
        _userRoleRepository = userRoleRepository;
        _storageService = storageService;
        _permissionRepository = permissionRepository;
        _emailService = emailService;
        _userVerificationTokenRepository = userVerificationTokenRepository;
        _httpContextAccessor = httpContextAccessor;
        _hostSettings = hostSettings.Value;
        _emailTemplateService = emailTemplateService;
        _menuRepository = menuRepository;
        _roleRepository = roleRepository;
        _logger = loggerFactory.CreateLogger<AuthService>();
        _fileUploadRepository = fileUploadRepository;
        _systemConfigRepository = systemConfigRepository;
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

    public async Task<ApiResponse> AdminLoginAsync(LoginRequestDto obj)
    {
        return await Login(obj, true);
    }

    public async Task<ApiResponse> ForgotPasswordAsync(string email, bool isClientRequest = false)
    {
        var user = await _userRepository
            .FirstOrDefaultAsync(x => x.Email.ToLower() == email.ToLower());

        if (user == null)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.EmailNotFound), ApiCodeConstants.Auth.EmailNotFound);


        if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.NotActivated))
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotActivated), ApiCodeConstants.Auth.UserNotActivated);

        if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Deactivated))
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserDeactivated), ApiCodeConstants.Auth.UserDeactivated);

        if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Locked))
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserDeactivated), ApiCodeConstants.Auth.UserLocked);

        var currentDate = DateTime.Now.Date;
        var requestCountToday = await _userVerificationTokenRepository
                                        .FindByCondition(x => x.UserId == user.Id &&
                                            x.Purpose == CommonConstants.UserVerificationTokenPurpose.FORGOT_PASSWORD &&
                                            x.CreatedDate.Date == currentDate)
                                        .Select(x => x.Id)
                                        .CountAsync();

        if (requestCountToday >= AuthConstants.MAX_ACCESS_FAILED)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.ForgotPasswordReachToLimit), ApiCodeConstants.Auth.ForgotPasswordReachToLimit);

        // Brand hệ thống dùng chung cho cả 2 nhánh (admin/client).
        var (systemName, logoUrl) = await GetSystemBrandAsync();

        // ── ADMIN: sinh mật khẩu mới, gửi qua email, buộc đổi ở lần đăng nhập kế tiếp ──
        if (!isClientRequest)
        {
            var newPassword = RandomHelper.GeneratePassword(12);
            user.PasswordHash = PasswordHelper.HashPassword(newPassword);
            user.MustChangePassword = true;
            user.LastModifiedDate = DateTime.Now;
            await _userRepository.UpdateAsync(user);

            // Lưu vết yêu cầu (đánh dấu đã dùng) để phục vụ giới hạn số lần/ngày.
            await _userVerificationTokenRepository.CreateAsync(new UserVerificationToken
            {
                UserId = user.Id,
                Code = RandomHelper.GenerateRandomString(50),
                Purpose = CommonConstants.UserVerificationTokenPurpose.FORGOT_PASSWORD,
                ExpirationDate = DateTime.Now.AddHours(AuthConstants.FORGOT_PASSWORD_TOKEN_EXPIRE_HOURS),
                IsUsed = true,
                CreatedDate = DateTime.Now
            });

            await _userRepository.SaveChangesAsync();
            await _userVerificationTokenRepository.SaveChangesAsync();

            var resetModel = new ResetPasswordEmailDto
            {
                SystemName = systemName,
                LogoUrl = logoUrl,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                Password = newPassword,
                LoginLink = _hostSettings.AdminUrl,
                Year = DateTime.Now.Year.ToString()
            };

            var resetBody = await _emailTemplateService
                .GetEmailTemplateAsync(AuthConstants.EmailTemplates.ADMIN_RESET_PASSWORD, resetModel);

            var resetRequest = new GoogleMailRequest
            {
                ToEmails = new List<string> { email },
                Subject = AuthConstants.NEW_PASSWORD_EMAIL_TITLE,
                Body = resetBody,
                CcEmails = new List<string>(),
                BccEmails = new List<string>()
            };

            await _emailService.SendMailAsync(resetRequest);

            return ApiResponse.Success();
        }

        // ── CLIENT: giữ nguyên luồng gửi mã OTP qua email ──
        var randomCode = RandomHelper.GenerateOtpCode();
        var newToken = new UserVerificationToken
        {
            UserId = user.Id,
            Code = randomCode,
            Purpose = CommonConstants.UserVerificationTokenPurpose.FORGOT_PASSWORD,
            ExpirationDate = DateTime.Now.AddHours(AuthConstants.FORGOT_PASSWORD_TOKEN_EXPIRE_HOURS),
            IsUsed = false,
            CreatedDate = DateTime.Now
        };
        await _userVerificationTokenRepository.CreateAsync(newToken);
        await _userVerificationTokenRepository.SaveChangesAsync();

        var host = _hostSettings.ClientUrl;
        var resetUrl = $"{host}/dat-lai-mat-khau?code={randomCode}&email={email}&purpose={newToken.Purpose}";

        var model = new AdminForgotPasswordEmailDto
        {
            FullName = $"{user.FirstName} {user.LastName}",
            ActiveLink = resetUrl,
            ValidExpired = AuthConstants.FORGOT_PASSWORD_TOKEN_EXPIRE_HOURS.ToString(),
            DateTime = DateTime.Now.Year.ToString(),
            Link = host,
            OtpCode = randomCode,
            SystemName = systemName,
            LogoUrl = logoUrl
        };

        var emailBody = await _emailTemplateService
            .GetEmailTemplateAsync(AuthConstants.EmailTemplates.CLIENT_FORGOT_PASSWORD_OTP, model);

        var emailRequest = new GoogleMailRequest
        {
            ToEmails = new List<string> { email },
            Subject = AuthConstants.FORGOT_PASSWORD_EMAIL_TITLE,
            Body = emailBody,
            CcEmails = new List<string>(),
            BccEmails = new List<string>()
        };

        await _emailService.SendMailAsync(emailRequest);

        return ApiResponse.Success(emailRequest);
    }

    public async Task<ApiResponse> GetProfileAsync(int userId)
    {
        var user = await _userRepository
            .FindByCondition(x => x.Id == userId)
            .Select(x => new AuthProfileResponseDto
            {
                Id = x.Id,
                Email = x.Email,
                FirstName = x.FirstName,
                Gender = x.Gender,
                LastName = x.LastName,
                PhoneNumber = x.PhoneNumber,
                Username = x.Username,
                UserStatus = new DataItem<int>
                {
                    Id = x.UserStatus.Id,
                    Name = x.UserStatus.Name,
                },
                UserRoles = x.UserRoles
                    .Where(x => !x.IsDeleted)
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

    public async Task<ApiResponse> LoginAsync(LoginRequestDto obj)
    {
        return await Login(obj, false);
    }

    private async Task<ApiResponse> Login(LoginRequestDto obj, bool isForAdmin)
    {
        var userInfo = await _userRepository
                        .FindByCondition(x => x.Username.ToLower() == obj.Username.ToLower() || x.Email.ToLower() == obj.Username.ToLower() ||
                            (!isForAdmin && string.IsNullOrEmpty(x.PhoneNumber) && x.PhoneNumber == obj.Username)
                        )
                        .Select(x => new
                        {
                            User = x,
                            AvatarUrl = x.Avatar == null ? null : _storageService.GetOriginalUrl(x.Avatar.FileKey)
                        })
                        .FirstOrDefaultAsync();

        if (userInfo == null)
            return ApiResponse.NotFound(userInfo, ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound), ApiCodeConstants.Auth.UserNotFound);

        var user = userInfo.User;
        var listRoleIds = new List<int>();
        var listRoles = new List<DataItem<int>>();
        if (isForAdmin)
        {
            listRoles = await (from a in _userRoleRepository.GetAll()
                               join b in _roleRepository.GetAll() on a.RoleId equals b.Id
                               where a.UserId == user.Id
                               select new DataItem<int>
                               {
                                   Id = a.RoleId,
                                   Name = b.Name
                               })
                            .ToListAsync();

            listRoleIds = listRoles
                .Select(x => x.Id)
                .ToList();

            // Mọi vai trò nghiệp vụ (Chủ kho/Thu mua/Kho/Xay/Bán hàng + Admin) đều dùng hệ thống quản lý.
            // Chỉ chặn khi tài khoản chưa được gán vai trò nào.
            if (!listRoleIds.Any())
                return ApiResponse.Forbidden(message: ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.RequiredAdminUser), code: ApiCodeConstants.Auth.RequiredAdminUser);
        }

        // #14: Kiểm tra khóa tài khoản TRƯỚC khi verify mật khẩu.
        // Nếu đang bị khóa và còn hiệu lực -> trả thông báo khóa, KHÔNG tăng AccessFailedCount
        // (tránh mỗi lần nhập sai lại gia hạn khóa vô hạn). Hết hạn khóa -> tự mở khóa rồi cho đăng nhập tiếp.
        if (user.LockEnabled && user.AccessFailedCount >= AuthConstants.MAX_ACCESS_FAILED)
        {
            if (user.LockEndDate.HasValue && user.LockEndDate.Value > DateTime.Now)
            {
                return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserLocked)
                        .Replace("{ExpireTime}", user.LockEndDate?.ToString("dd/MM/yyy HH:mm:ss")),
                    ApiCodeConstants.Auth.UserLocked);
            }

            user.LockEnabled = false;
            user.LockEndDate = null;
            user.AccessFailedCount = 0;
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();
        }

        if (!PasswordHelper.VerifyPassword(obj.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= AuthConstants.MAX_ACCESS_FAILED)
            {
                user.UserStatusId = Lookup.UserStatusId(LookupCodes.UserStatus.Locked);
                user.LockEnabled = true;
                user.LockEndDate = DateTime.Now.AddHours(AuthConstants.EXPIRE_TIME_LOCKED);

                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();

                return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserLocked)
                        .Replace("{ExpireTime}", user.LockEndDate?.ToString("dd/MM/yyy HH:mm:ss")),
                    ApiCodeConstants.Auth.UserLocked
                );
            }
            await _userRepository.UpdateAsync(user);
            await _userRepository.SaveChangesAsync();

            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound), ApiCodeConstants.Auth.UserNotFound);
        }

        if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.NotActivated))
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotActivated), ApiCodeConstants.Auth.UserNotActivated);

        if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Deactivated))
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserDeactivated), ApiCodeConstants.Auth.UserDeactivated);

        if (user.LockEnabled)
        {
            if (user.LockEndDate <= DateTime.Now || user.AccessFailedCount < AuthConstants.MAX_ACCESS_FAILED)
            {
                user.LockEndDate = null;
                user.LockEnabled = false;
                user.AccessFailedCount = 0;

                await _userRepository.UpdateAsync(user);
                await _userRepository.SaveChangesAsync();
            }
            else
            {
                return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserLocked)
                    .Replace("{ExpireTime}", user.LockEndDate?.ToString("dd/MM/yyy HH:mm:ss")),
                   ApiCodeConstants.Auth.UserLocked
                );
            }
        }
        else
        {
            user.LockEndDate = null;
            user.LockEnabled = false;
            user.AccessFailedCount = 0;
            user.UserStatusId = Lookup.UserStatusId(LookupCodes.UserStatus.Active);
        }

        var userToken = new UserToken
        {
            Id = user.Id,
            Email = user.Email,
            Phone = user.PhoneNumber ?? string.Empty,
            Username = user.Username,
            AccessTokenJti = Guid.NewGuid().ToString(),
            RoleIds = listRoleIds
        };
        var accessToken = _tokenProviderService.GenerateToken(userToken);
        var (refreshToken, refreshTokenTtl) = _tokenProviderService.GenerateRefreshToken();

        user.LastLoginDate = DateTime.Now;
        await _userRepository.UpdateAsync(user);

        var userSession = new UserSession
        {
            AccessTokenJti = userToken.AccessTokenJti,
            ExpirationDate = refreshTokenTtl,
            IsRevoked = false,
            IsUsed = false,
            RefreshToken = refreshToken,
            UserId = user.Id,
        };
        await _userSessionRepository.CreateAsync(userSession);
        await _userSessionRepository.SaveChangesAsync();

        if (isForAdmin)
        {
            var permissions = await _userRepository.GetPermissionsAsync(user.Id);

            //Lấy menu
            var menus = await _userRepository.GetMenuAsync(user.Id);

            var loginResponse = new LoginResponseDto<LoginResponseAdminUserInfo>
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserInfo = new LoginResponseAdminUserInfo
                {
                    Id = user.Id,
                    FullName = user.FirstName + " " + user.LastName,
                    Email = user.Email,
                    AvatarUrl = userInfo.AvatarUrl,
                    Roles = listRoles,
                    Permissions = permissions,
                    Menus = menus,
                    MustChangePassword = user.MustChangePassword,
                }
            };

            return ApiResponse.Success(loginResponse);
        }
        else
        {
            var loginResponse = new LoginResponseDto<LoginResponseClientUserInfo>
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                UserInfo = new LoginResponseClientUserInfo
                {
                    Id = user.Id,
                    FullName = user.FirstName + " " + user.LastName,
                    Email = user.Email,
                    AvatarUrl = userInfo.AvatarUrl,
                    MustChangePassword = user.MustChangePassword,
                }
            };

            return ApiResponse.Success(loginResponse);
        }
    }

    public async Task<ApiResponse> LogoutAllDeviceAsync(int userId)
    {
        var userSessions = await _userSessionRepository
            .FindByConditionAsync(x => x.UserId == userId && !x.IsUsed && !x.IsRevoked);

        if (userSessions.Any())
        {
            foreach (var item in userSessions)
            {
                item.IsRevoked = true;
            }

            await _userSessionRepository.UpdateListAsync(userSessions);
            await _userSessionRepository.SaveChangesAsync();
        }

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> LogoutAsync(LogoutRequestDto obj, int userId)
    {
        var userSession = await _userSessionRepository
            .FindByCondition(x => x.UserId == userId && x.RefreshToken == obj.RefreshToken)
            .FirstOrDefaultAsync();
        if (userSession == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.NotFound), ApiCodeConstants.Common.NotFound);

        userSession.IsRevoked = true;

        await _userSessionRepository.UpdateAsync(userSession);
        await _userSessionRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> RefreshTokenAsync(RefreshTokenRequestDto obj)
    {
        var isValidToken = _tokenProviderService.ValidateToken(obj.AccessToken, false);
        if (!isValidToken)
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidToken), ApiCodeConstants.Auth.InvalidToken);

        var token = _tokenProviderService.ParseToken(obj.AccessToken);
        if (token == null)
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidToken), ApiCodeConstants.Auth.InvalidToken);

        if (token.ValidTo > DateTime.UtcNow.AddMinutes(5))
            return ApiResponse.BadRequest(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.AccessTokenNotExpired),
                ApiCodeConstants.Auth.AccessTokenNotExpired
            );

        var jti = token.Claims.FirstOrDefault(c => c.Type == ClaimNames.JTI)?.Value;
        var userSession = await _userSessionRepository
            .FindByCondition(x => x.RefreshToken == obj.RefreshToken && x.AccessTokenJti == jti)
            .FirstOrDefaultAsync();
        if (userSession == null)
            return ApiResponse.Unauthorized(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidRefreshToken),
                ApiCodeConstants.Auth.InvalidRefreshToken
            );
        if (userSession.IsUsed)
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.RefreshTokenIsUsed), ApiCodeConstants.Auth.RefreshTokenIsUsed);
        if (userSession.IsRevoked)
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.AccessTokenRevoked), ApiCodeConstants.Auth.AccessTokenRevoked);
        if (userSession.ExpirationDate <= DateTime.Now.AddMinutes(-5))
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.RefreshTokenExpired), ApiCodeConstants.Auth.RefreshTokenExpired);
        var userId = Convert.ToInt32(token.Claims.FirstOrDefault(c => c.Type == ClaimNames.ID)?.Value);
        var accessTokenJti = Guid.NewGuid().ToString();
        var userToken = await _userRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == userId)
            .Select(x => new UserToken
            {
                Id = x.Id,
                Email = x.Email,
                Phone = x.PhoneNumber ?? string.Empty,
                Username = x.Username,
                AccessTokenJti = accessTokenJti,
                FullName = x.FirstName + " " + x.LastName,
                RoleIds = x.UserRoles
                    .Where(x => !x.IsDeleted)
                    .Select(xx => xx.RoleId)
                    .ToList()
            })
            .FirstOrDefaultAsync();
        if (userToken == null)
            return ApiResponse.Unauthorized(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.InvalidToken), ApiCodeConstants.Auth.InvalidToken);

        var accessToken = _tokenProviderService.GenerateToken(userToken);
        var (refreshToken, refreshTokenTtl) = _tokenProviderService.GenerateRefreshToken();
        var refreshTokenResponse = new RefreshTokenResponseDto
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken
        };

        userSession.IsUsed = true;
        userSession.LastModifiedDate = DateTime.Now;
        userSession.UpdatedBy = userId;
        await _userSessionRepository.UpdateAsync(userSession);

        var newUserSession = new UserSession
        {
            AccessTokenJti = userToken.AccessTokenJti,
            ExpirationDate = refreshTokenTtl,
            IsRevoked = false,
            IsUsed = false,
            RefreshToken = refreshTokenResponse.RefreshToken,
            UserId = userToken.Id,
            CreatedBy = userId,
        };
        await _userSessionRepository.CreateAsync(newUserSession);

        await _userSessionRepository.SaveChangesAsync();

        return ApiResponse.Success(refreshTokenResponse);
    }

    public async Task<ApiResponse> RegisterAsync(UserSignUpDto obj)
    {
        //Check valid username
        var isValidUsername = StringHelper.IsValidUsername(obj.UserName);
        if (!isValidUsername)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData).Replace("{PropertyName}", "Tên đăng nhập"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng tên đăng nhập
        var isExistUsername = await _userRepository.AnyAsync(x => !x.IsDeleted && x.Username == obj.UserName);

        if (isExistUsername)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.UserName),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        //Check valid email
        var isValidEmail = EmailHelper.IsValidEmail(obj.Email);
        if (!isValidEmail)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Email"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng email
        var isExistEmail = await _userRepository.AnyAsync(x => !x.IsDeleted && x.Email == obj.Email);
        if (isExistEmail)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Email),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        //Check valid phoneNumber
        var isValidPhoneNumber = PhoneHelper.IsValidVietnamPhone(obj.PhoneNumber);
        if (!isValidPhoneNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Số điện thoại"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng phoneNumber
        var isExistPhoneNumber = await _userRepository.AnyAsync(x => !x.IsDeleted && x.PhoneNumber == obj.PhoneNumber);

        if (isExistPhoneNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.PhoneNumber),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        //Check valid IdentityNumber
        var isValidIdentityNumber = StringHelper.IsValidIdentityNumber(obj.IdentityNumber);
        if (!isValidIdentityNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Số căn cước công dân"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng IdentityNumber
        var isExistIdentityNumber = await _userRepository.AnyAsync(x => !x.IsDeleted && x.IdentityNumber == obj.IdentityNumber);

        if (isExistIdentityNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.IdentityNumber),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        var model = obj.ToEntity();
        try
        {
            //Bắt đầu transaction
            await _userRepository.BeginTransactionAsync();
            await _userRepository.CreateAsync(model);
            await _userRepository.SaveChangesAsync();

            //Kết thúc transaction
            await _userRepository.EndTransactionAsync();

            //Gửi email kích hoạt
            var randomCode = RandomHelper.GenerateRandomString(50);
            var newToken = new UserVerificationToken
            {
                UserId = model.Id,
                Code = randomCode,
                Purpose = CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION,
                ExpirationDate = DateTime.Now.AddHours(AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME),
                IsUsed = false,
                CreatedDate = DateTime.Now
            };

            await _userVerificationTokenRepository.CreateAsync(newToken);
            await _userVerificationTokenRepository.SaveChangesAsync();

            var apiBase = _hostSettings.ApiUrl;
            var resetUrl = $"{apiBase}/auth/activate?code={Uri.EscapeDataString(randomCode)}&email={Uri.EscapeDataString(model.Email)}&purpose={CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION}";

            var (systemName, logoUrl) = await GetSystemBrandAsync();
            var modelEmail = new UserActivationDto
            {
                FullName = $"{model.FirstName} {model.LastName}",
                ActiveLink = resetUrl,
                ValidExpired = AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME.ToString(),
                DateTime = DateTime.Now.Year.ToString(),
                Link = apiBase,
                SystemName = systemName,
                LogoUrl = logoUrl
            };

            var emailBody = await _emailTemplateService.GetEmailTemplateAsync(AuthConstants.EmailTemplates.ADMIN_ACCOUNT_ACTIVATION, modelEmail);

            var emailRequest = new GoogleMailRequest
            {
                ToEmails = new List<string> { model.Email },
                Subject = AuthConstants.ACCOUNT_ACTIVATION_EMAIL_TITLE,
                Body = emailBody,
                CcEmails = new List<string>(),
                BccEmails = new List<string>()
            };

            await _emailService.SendMailAsync(emailRequest);

            ApiResponse.Success(emailRequest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> AdminRegisterAsync(AdminRegisterDto obj)
    {
        //Check trùng tên đăng nhập
        var isExistUsername = await _userRepository.AnyAsync(x => !x.IsDeleted &&
        x.Username == obj.Username &&
        x.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active));

        if (isExistUsername) return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Username),
                ApiCodeConstants.Common.DuplicatedData
            );

        //Check valid username
        var isValidUsername = StringHelper.IsValidUsername(obj.Username);
        if (!isValidUsername) return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData).Replace("{PropertyName}", "Tên đăng nhập"),
                ApiCodeConstants.Common.InvalidData
            );

        //Check trùng email
        var isExistEmail = await _userRepository.AnyAsync(x => !x.IsDeleted &&
        x.Email == obj.Email &&
        x.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active));
        if (isExistEmail) return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Email),
                ApiCodeConstants.Common.DuplicatedData
            );

        //Check valid email
        var isValidEmail = EmailHelper.IsValidEmail(obj.Email);
        if (!isValidEmail)
        {
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData).Replace("{PropertyName}", "Email"),
                ApiCodeConstants.Common.InvalidData
            );
        }
        var model = obj.ToEntity();
        try
        {
            //Bắt đầu transaction
            await _userRepository.BeginTransactionAsync();
            await _userRepository.CreateAsync(model);
            await _userRepository.SaveChangesAsync();

            //Thêm userRole — mặc định vai trò Nhân viên kho; admin có thể đổi lại trong quản lý người dùng.
            var userRole = new UserRole()
            {
                UserId = model.Id,
                RoleId = CommonConstants.Role.WAREHOUSE,
                CreatedDate = DateTime.Now,
            };
            await _userRoleRepository.CreateAsync(userRole);
            await _userRoleRepository.SaveChangesAsync();
            //Kết thúc transaction
            await _userRepository.EndTransactionAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
        return ApiResponse.Created(model.Id);
    }
    public async Task<ApiResponse> ResetPasswordAsync(ResetPasswordDto dto)
    {
        if (dto.NewPassword != dto.ConfirmPassword)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword), ApiCodeConstants.Auth.ConfirmPasswordNotMatchPassword);

        var user = await _userRepository
            .FindByCondition(x => x.Email == dto.Email && !x.IsDeleted)
            .FirstOrDefaultAsync();

        if (user == null)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.EmailNotFound), ApiCodeConstants.Auth.EmailNotFound);

        var token = await _userVerificationTokenRepository
            .FindByCondition(x => x.UserId == user.Id &&
                x.Code == dto.Code &&
                x.Purpose == dto.Purpose &&
                !x.IsUsed &&
                x.ExpirationDate > DateTime.Now)
            .FirstOrDefaultAsync();

        if (token == null)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.VerificationCodeHasExpired), ApiCodeConstants.Auth.VerificationCodeHasExpired);


        user.PasswordHash = PasswordHelper.HashPassword(dto.NewPassword);
        user.LastModifiedDate = DateTime.Now;

        token.IsUsed = true;

        await _userRepository.UpdateAsync(user);
        await _userVerificationTokenRepository.UpdateAsync(token);

        await _userRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> VerifyCodeAsync(VerifyCodeDto dto)
    {
        var user = await _userRepository
            .FirstOrDefaultAsync(x => x.Email.ToLower() == dto.Email.ToLower());

        if (user == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.EmailNotFound), ApiCodeConstants.Auth.EmailNotFound);

        var token = await _userVerificationTokenRepository
            .FirstOrDefaultAsync(x => x.UserId == user.Id &&
                x.Code == dto.Code &&
                x.Purpose == dto.Purpose);

        if (token == null)
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.NotFound), ApiCodeConstants.Common.NotFound);

        if (token.IsUsed)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.VerificationCodeUsed), ApiCodeConstants.Auth.VerificationCodeUsed);

        if (token.ExpirationDate < DateTime.Now)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.VerificationCodeHasExpired), ApiCodeConstants.Auth.VerificationCodeHasExpired);

        if (dto.Purpose == CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION)
        {
            user.UserStatusId = Lookup.UserStatusId(LookupCodes.UserStatus.Active);
            await _userRepository.UpdateAsync(user);

            token.IsUsed = true;
            await _userVerificationTokenRepository.UpdateAsync(token);

            await _userVerificationTokenRepository.SaveChangesAsync();
        }

        return ApiResponse.Success(token);
    }

    public async Task<ApiResponse> AdminCreateEndUser(CreateEndUserDto obj)
    {
        //Check valid username
        var isValidUsername = StringHelper.IsValidUsername(obj.Username);
        if (!isValidUsername)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData).Replace("{PropertyName}", "Tên đăng nhập"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng tên đăng nhập
        var isExistUsername = await _userRepository.AnyAsync(x => !x.IsDeleted && x.Username == obj.Username);

        if (isExistUsername)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Username),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        //Check valid email
        var isValidEmail = EmailHelper.IsValidEmail(obj.Email);
        if (!isValidEmail)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Email"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng email
        var isExistEmail = await _userRepository.AnyAsync(x => !x.IsDeleted && x.Email == obj.Email);
        if (isExistEmail)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Email),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        //Check valid phoneNumber
        if (!String.IsNullOrEmpty(obj.PhoneNumber))
        {
            var isValidPhoneNumber = PhoneHelper.IsValidVietnamPhone(obj.PhoneNumber);
            if (!isValidPhoneNumber)
            {
                return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Số điện thoại"),
                    ApiCodeConstants.Common.InvalidData
                );
            }

            //Check trùng phoneNumber
            var isExistPhoneNumber = await _userRepository.AnyAsync(x => !x.IsDeleted && x.PhoneNumber == obj.PhoneNumber);

            if (isExistPhoneNumber)
            {
                return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.PhoneNumber),
                    ApiCodeConstants.Common.DuplicatedData
                );
            }
        }

        //Check valid IdentityNumber
        var isValidIdentityNumber = StringHelper.IsValidIdentityNumber(obj.IdentityNumber);
        if (!isValidIdentityNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidFormatMessage).Replace("{PropertyName}", "Số căn cước công dân"),
                ApiCodeConstants.Common.InvalidData
            );
        }

        //Check trùng IdentityNumber
        var isExistIdentityNumber = await _userRepository.AnyAsync(x => !x.IsDeleted && x.IdentityNumber == obj.IdentityNumber);

        if (isExistIdentityNumber)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.IdentityNumber),
                ApiCodeConstants.Common.DuplicatedData
            );
        }

        var model = obj.ToEntity();
        try
        {
            //Bắt đầu transaction
            await _userRepository.BeginTransactionAsync();
            await _userRepository.CreateAsync(model);
            await _userRepository.SaveChangesAsync();

            //Thêm userRole — mặc định vai trò Nhân viên kho; admin có thể đổi lại trong quản lý người dùng.
            var userRole = new UserRole()
            {
                UserId = model.Id,
                RoleId = CommonConstants.Role.WAREHOUSE,
                CreatedDate = DateTime.Now,
            };
            await _userRoleRepository.CreateAsync(userRole);
            await _userRoleRepository.SaveChangesAsync();
            //Kết thúc transaction
            await _userRepository.EndTransactionAsync();

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create user with message {Message}", ex.Message);
            await _userRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
        return ApiResponse.Created(model.Id);
    }

    public Task<ApiResponse> GetCurrentUserDecentralization()
    {
        try
        {
            return Task.FromResult(ApiResponse.Success(new DecentralizationDto
            {
                UserRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds() ?? [],
                UserId = _httpContextAccessor.HttpContext?.GetCurrentUserId(),
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to get current user decentralization: {Message}", ex.Message);
            return Task.FromResult(ApiResponse.InternalServerError());
        }
    }
    /// <summary>
    /// Lấy danh sách menu theo phân quyền của người dùng hiện tại.
    /// Dùng đúng logic dựng menu như khi đăng nhập (GetMenuAsync) nên xử lý
    /// chính xác cả role isCheckAll. Cho phép FE làm mới sidebar theo quyền
    /// mà không cần đăng nhập lại hay tải lại trang.
    /// </summary>
    public async Task<ApiResponse> GetCurrentUserMenusAsync(int userId)
    {
        try
        {
            var menus = await _userRepository.GetMenuAsync(userId);
            return ApiResponse.Success(menus);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to get current user menus: {Message}", ex.Message);
            return ApiResponse.InternalServerError();
        }
    }

    /// <summary>
    /// Dựng lại userInfo (profile + roles + permissions + menus) cho user hiện tại — cùng shape với login.
    /// FE gọi khi khởi động (còn token) để nạp phân quyền vào bộ nhớ, không lưu ở localStorage.
    /// </summary>
    public async Task<ApiResponse> GetCurrentUserSessionAsync(int userId)
    {
        try
        {
            var userInfo = await _userRepository
                .FindByCondition(x => x.Id == userId)
                .Select(x => new
                {
                    User = x,
                    AvatarUrl = x.Avatar == null ? null : _storageService.GetOriginalUrl(x.Avatar.FileKey)
                })
                .FirstOrDefaultAsync();

            if (userInfo == null)
                return ApiResponse.NotFound(
                    ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotFound),
                    ApiCodeConstants.Auth.UserNotFound);

            var user = userInfo.User;

            var listRoles = await (from a in _userRoleRepository.GetAll()
                                   join b in _roleRepository.GetAll() on a.RoleId equals b.Id
                                   where a.UserId == user.Id
                                   select new DataItem<int>
                                   {
                                       Id = a.RoleId,
                                       Name = b.Name
                                   })
                                .ToListAsync();

            var permissions = await _userRepository.GetPermissionsAsync(user.Id);
            var menus = await _userRepository.GetMenuAsync(user.Id);

            var result = new LoginResponseAdminUserInfo
            {
                Id = user.Id,
                FullName = user.FirstName + " " + user.LastName,
                Email = user.Email,
                AvatarUrl = userInfo.AvatarUrl,
                Roles = listRoles,
                Permissions = permissions,
                Menus = menus,
                MustChangePassword = user.MustChangePassword,
            };

            return ApiResponse.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fail to get current user session: {Message}", ex.Message);
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> ResendActivationMailAsync(ResendActivationMailDto dto)
    {
        try
        {
            var user = await _userRepository
                .FindByCondition(x => x.Email.ToLower() == dto.Email.ToLower() && !x.IsDeleted)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return ApiResponse.BadRequest();
            }

            if (user.UserStatusId == Lookup.UserStatusId(LookupCodes.UserStatus.Active))
            {
                return ApiResponse.BadRequest();
            }

            var existData = await _userVerificationTokenRepository
                .FirstOrDefaultAsync(x => x.Code == dto.VerificationCode && x.UserId == user.Id);
            if (existData == null)
                return ApiResponse.BadRequest();

            if (existData.ExpirationDate < DateTime.Now || existData.IsUsed)
                return ApiResponse.BadRequest();

            await _userVerificationTokenRepository
                .SoftDeleteAsync(x => x.UserId == user.Id &&
                    x.Purpose == CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION &&
                    !x.IsUsed && !x.IsDeleted);

            var randomCode = RandomHelper.GenerateRandomString(50);
            var newToken = new UserVerificationToken
            {
                UserId = user.Id,
                Code = randomCode,
                Purpose = CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION,
                ExpirationDate = DateTime.Now.AddHours(AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME),
                IsUsed = false,
                CreatedDate = DateTime.Now
            };

            await _userVerificationTokenRepository.CreateAsync(newToken);
            await _userVerificationTokenRepository.SaveChangesAsync();

            var apiBase = _hostSettings.ApiUrl;
            var resetUrl = $"{apiBase}/auth/activate?code={Uri.EscapeDataString(randomCode)}&email={Uri.EscapeDataString(user.Email)}&purpose={CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION}";

            var (systemName, logoUrl) = await GetSystemBrandAsync();
            var modelEmail = new UserActivationDto
            {
                FullName = $"{user.FirstName} {user.LastName}",
                ActiveLink = resetUrl,
                ValidExpired = AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME.ToString(),
                DateTime = DateTime.Now.Year.ToString(),
                Link = apiBase,
                SystemName = systemName,
                LogoUrl = logoUrl
            };

            var emailBody = await _emailTemplateService.GetEmailTemplateAsync(AuthConstants.EmailTemplates.ADMIN_ACCOUNT_ACTIVATION, modelEmail);

            var emailRequest = new GoogleMailRequest
            {
                ToEmails = new List<string> { user.Email },
                Subject = AuthConstants.ACCOUNT_ACTIVATION_EMAIL_TITLE,
                Body = emailBody,
                CcEmails = new List<string>(),
                BccEmails = new List<string>()
            };

            await _emailService.SendMailAsync(emailRequest);

            return ApiResponse.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to resend activation mail with message {Message}", ex.Message);
            return ApiResponse.InternalServerError();
        }
    }
}
