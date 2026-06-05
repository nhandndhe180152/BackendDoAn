using System;
using System.Text.RegularExpressions;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Options;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.Users;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
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

    public UserService(IUserRepository userRepository, IUserRoleRepository userRoleRepository, IMenuRepository menuRepository, IPermissionRepository permissionRepository, ILoggerFactory loggerFactory, IStorageService storageService, IUserVerificationTokenRepository userVerificationTokenRepository, IOptions<HostSettings> hostSettings, IEmailTemplateService emailTemplateService, IEmailService<GoogleMailRequest> emailService, IHttpContextAccessor httpContextAccessor, IUserSessionRepository userSessionRepository)
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
    }

    public async Task<ApiResponse> CreateAsync(CreateUserDto obj)
    {
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

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateUserDto> objs)
    {
        var model = objs.Select(x => x.ToEntity());

        await _userRepository.CreateListAsync(model);
        await _userRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Select(x => x.Id));
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

        if ((existData.UserStatusId == (int)Enums.UserStatus.Locked || existData.LockEnabled) && obj.UserStatusId == (int)Enums.UserStatus.Actived)
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

    public async Task<ApiResponse> GetPagedEndUserAsync(SearchQuery query)
    {
        var data = (from u in _userRepository.GetAll()
                    join ur in _userRoleRepository.GetAll() on u.Id equals ur.UserId
                    where !u.IsDeleted && !ur.IsDeleted && ur.RoleId == CommonConstants.Role.END_USER && u.UserStatusId == (int)Enums.UserStatus.Actived
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

        user.UserStatusId = (int)Enums.UserStatus.Deactivated;
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
                          where !u.IsDeleted && !ur.IsDeleted && ur.RoleId == CommonConstants.Role.END_USER && u.UserStatusId == (int)Enums.UserStatus.Actived
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
