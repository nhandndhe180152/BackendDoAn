using System;
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

    public AuthService(IUserRepository userRepository, IUserSessionRepository userSessionRepository, ITokenProviderService tokenProviderService, IUserRoleRepository userRoleRepository, IStorageService storageService, IPermissionRepository permissionRepository, IEmailService<GoogleMailRequest> emailService, IUserVerificationTokenRepository userVerificationTokenRepository, IHttpContextAccessor httpContextAccessor, IOptions<HostSettings> hostSettings, IEmailTemplateService emailTemplateService, IMenuRepository menuRepository, IRoleRepository roleRepository, ILoggerFactory loggerFactory, IFileUploadRepository fileUploadRepository)
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


        if (user.UserStatusId == (int)Enums.UserStatus.NotActivated)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotActivated), ApiCodeConstants.Auth.UserNotActivated);

        if (user.UserStatusId == (int)Enums.UserStatus.Deactivated)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserDeactivated), ApiCodeConstants.Auth.UserDeactivated);

        if (user.UserStatusId == (int)Enums.UserStatus.Locked)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserDeactivated), ApiCodeConstants.Auth.UserLocked);

        var randomCode = isClientRequest ? RandomHelper.GenerateOtpCode() : RandomHelper.GenerateRandomString(50);
        var currentDate = DateTime.Now.Date;
        var requestCountToday = await _userVerificationTokenRepository
                                        .FindByCondition(x => x.UserId == user.Id &&
                                            x.Purpose == CommonConstants.UserVerificationTokenPurpose.FORGOT_PASSWORD &&
                                            x.CreatedDate.Date == currentDate)
                                        .Select(x => x.Id)
                                        .CountAsync();

        if (requestCountToday >= AuthConstants.MAX_ACCESS_FAILED)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.ForgotPasswordReachToLimit), ApiCodeConstants.Auth.ForgotPasswordReachToLimit);


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

        var host = isClientRequest ? _hostSettings.ClientUrl : _hostSettings.AdminUrl;
        var resetUrl = isClientRequest
            ? $"{host}/dat-lai-mat-khau?code={randomCode}&email={email}&purpose={newToken.Purpose}"
            : $"{host}/reset-password?code={randomCode}&email={email}&purpose={newToken.Purpose}";


        var model = new AdminForgotPasswordEmailDto
        {
            FullName = $"{user.FirstName} {user.LastName}",
            ActiveLink = resetUrl,
            ValidExpired = AuthConstants.FORGOT_PASSWORD_TOKEN_EXPIRE_HOURS.ToString(),
            DateTime = DateTime.Now.Year.ToString(),
            Link = host,
            OtpCode = randomCode
        };

        var emailTemplate = isClientRequest
            ? AuthConstants.EmailTemplates.CLIENT_FORGOT_PASSWORD_OTP
            : AuthConstants.EmailTemplates.ADMIN_FORGOT_PASSWORD;
        var emailBody = await _emailTemplateService.GetEmailTemplateAsync(emailTemplate, model);


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

            if (!listRoleIds.Any(x => x != CommonConstants.Role.END_USER && x != CommonConstants.Role.DRIVER))
                return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.RequiredAdminUser), ApiCodeConstants.Auth.RequiredAdminUser);
        }

        if (!PasswordHelper.VerifyPassword(obj.Password, user.PasswordHash))
        {
            user.AccessFailedCount++;
            if (user.AccessFailedCount >= AuthConstants.MAX_ACCESS_FAILED)
            {
                user.UserStatusId = (int)Enums.UserStatus.Locked;
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

        if (user.UserStatusId == (int)Enums.UserStatus.NotActivated)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.UserNotActivated), ApiCodeConstants.Auth.UserNotActivated);

        if (user.UserStatusId == (int)Enums.UserStatus.Deactivated)
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
            user.UserStatusId = (int)Enums.UserStatus.Actived;
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
                    AvatarUrl = userInfo.AvatarUrl
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

            var modelEmail = new UserActivationDto
            {
                FullName = $"{model.FirstName} {model.LastName}",
                ActiveLink = resetUrl,
                ValidExpired = AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME.ToString(),
                DateTime = DateTime.Now.Year.ToString(),
                Link = apiBase
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
        x.UserStatusId == (int)Enums.UserStatus.Actived);

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
        x.UserStatusId == (int)Enums.UserStatus.Actived);
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

            //Thêm userRole
            var userRole = new UserRole()
            {
                UserId = model.Id,
                RoleId = CommonConstants.Role.DISPATCHER,
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
            return ApiResponse.NotFound(ErrorMessagesConstants.GetMessage(Common.NotFound), ApiCodeConstants.Common.NotFound);

        if (token.IsUsed)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.VerificationCodeUsed), ApiCodeConstants.Auth.VerificationCodeUsed);

        if (token.ExpirationDate < DateTime.Now)
            return ApiResponse.BadRequest(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Auth.VerificationCodeHasExpired), ApiCodeConstants.Auth.VerificationCodeHasExpired);

        if (dto.Purpose == CommonConstants.UserVerificationTokenPurpose.ACCOUNT_ACTIVATION)
        {
            user.UserStatusId = (int)Enums.UserStatus.Actived;
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

            //Thêm userRole
            var userRole = new UserRole()
            {
                UserId = model.Id,
                RoleId = CommonConstants.Role.END_USER,
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

            if (user.UserStatusId == (int)Enums.UserStatus.Actived)
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

            var modelEmail = new UserActivationDto
            {
                FullName = $"{user.FirstName} {user.LastName}",
                ActiveLink = resetUrl,
                ValidExpired = AuthConstants.ACCOUNT_ACTIVATION_EXPIRE_TIME.ToString(),
                DateTime = DateTime.Now.Year.ToString(),
                Link = apiBase
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
