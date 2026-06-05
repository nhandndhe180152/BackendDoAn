using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Permissions;
using Backend.Application.DTOs.Roles;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class RoleService : IRoleService
{
    private readonly IRoleRepository _roleRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IActionRepository _actionRepository;
    private readonly IMenuRepository _menuRepository;
    private readonly IUserRoleRepository _userRoleRepository;
    private readonly IActionInMenuRepository _actionInMenuRepository;
    private readonly ICacheService _cacheService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public RoleService(IRoleRepository roleRepository, IPermissionRepository permissionRepository, IActionRepository actionRepository, IMenuRepository menuRepository, IUserRoleRepository userRoleRepository, IActionInMenuRepository actionInMenuRepository, IHttpContextAccessor httpContextAccessor, ICacheService cacheService)
    {
        _roleRepository = roleRepository;
        _permissionRepository = permissionRepository;
        _actionRepository = actionRepository;
        _menuRepository = menuRepository;
        _userRoleRepository = userRoleRepository;
        _actionInMenuRepository = actionInMenuRepository;
        _httpContextAccessor = httpContextAccessor;
        _cacheService = cacheService;
    }

    public async Task<ApiResponse> CreateAsync(CreateRoleDto obj)
    {
        var isExistName = await _roleRepository.AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower());
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );
        var model = obj.ToEntity();

        try
        {
            await _roleRepository.BeginTransactionAsync();

            await _roleRepository.CreateAsync(model);
            await _roleRepository.SaveChangesAsync();

            var permissons = new List<Permission>();

            if (obj.IsCheckAll)
            {
                permissons = await _actionInMenuRepository
                    .FindByCondition(x => !x.IsDeleted)
                    .Select(x => new Permission
                    {
                        CreatedBy = obj.CreatedBy,
                        CreatedDate = DateTime.Now,
                        ActionId = x.ActionId,
                        MenuId = x.MenuId,
                        RoleId = model.Id
                    })
                    .ToListAsync();
            }
            else
            {
                if (obj.Permissions.Any())
                {
                    permissons = obj.Permissions
                    .Select(x => new Permission
                    {
                        ActionId = x.ActionId,
                        MenuId = x.MenuId,
                        RoleId = model.Id,
                        CreatedBy = model.CreatedBy,
                        CreatedDate = DateTime.Now
                    })
                    .ToList();
                }
            }

            if (permissons.Any())
            {
                await _permissionRepository.CreateListAsync(permissons);
                await _permissionRepository.SaveChangesAsync();
            }

            await _roleRepository.EndTransactionAsync();

            //update cache
            if (permissons.Any())
            {
                await _cacheService.RemoveAsync(CommonConstants.Cache.PERMISSIONS_ALL_KEY);
                var permissions = await _permissionRepository
                    .GetAllAsync();
                await _cacheService.SetAsync<List<Permission>>(CommonConstants.Cache.PERMISSIONS_ALL_KEY, permissions);
            }
        }
        catch
        {
            await _roleRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }


        return ApiResponse.Success(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateRoleDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _roleRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new RoleDetailDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Name = x.Name
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _roleRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        var dto = data.ToDto();

        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = await (from a in _roleRepository.GetAll()
                          join b in _permissionRepository.GetAll() on a.Id equals b.RoleId into groupAB
                          from b in groupAB.DefaultIfEmpty()
                          join c in _menuRepository.GetAll() on b.MenuId equals c.Id into groupBC
                          from c in groupBC.DefaultIfEmpty()
                          join d in _actionRepository.GetAll() on b.ActionId equals d.Id into groupCD
                          from d in groupCD.DefaultIfEmpty()
                          where !a.IsDeleted && (b == null || !b.IsDeleted) && (c == null || !c.IsDeleted) && (d == null || !d.IsDeleted)
                          select new
                          {
                              Id = a.Id,
                              Name = a.Name,
                              Description = a.Description,
                              CreatedDate = a.CreatedDate,
                              PermissionId = b != null ? b.Id : (int?)b.Id,
                              MenuName = c != null ? c.Name : string.Empty,
                              ActionName = d != null ? d.Name : string.Empty,
                              TotalUsers = a.UserRoles.Count(x => !x.IsDeleted)
                          })
                   .GroupBy(x => new
                   {
                       x.Id,
                       x.Name,
                       x.Description,
                       x.CreatedDate,
                       x.TotalUsers
                   })
                   .Select(x => new RoleListDto
                   {
                       Id = x.Key.Id,
                       CreatedDate = x.Key.CreatedDate,
                       Description = x.Key.Description,
                       Name = x.Key.Name,
                       TotalUser = x.Key.TotalUsers,
                       Permissons = x.Where(xx => xx.PermissionId.HasValue).Select(xx => new RolePermissonDto
                       {
                           Id = xx.PermissionId.Value,
                           ActionName = xx.ActionName,
                           MenuName = xx.MenuName,
                       })
                       .ToList()
                   })
                   .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        var data = _roleRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new RoleListDto
            {
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Id = x.Id,
                Name = x.Name,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<RoleListDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPermissionAsync(int roleId)
    {
        var data = await _roleRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == roleId)
            .Select(x => new RolePermissionDetailDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Name = x.Name,
                Permissions = x.Permissions
                    .Where(xx => !xx.IsDeleted)
                    .Select(xx => new RoleMenuActionDto
                    {
                        ActionId = xx.ActionId,
                        MenuId = xx.MenuId
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        //Không cho xoá những role mặc định
        if (id <= CommonConstants.Role.END_USER)
            return ApiResponse.Forbidden(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.Forbidden), ApiCodeConstants.Common.Forbidden);

        var isDeleted = await _roleRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _roleRepository.SaveChangesAsync();

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateRoleDto obj)
    {
        var isExistName = await _roleRepository
            .AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id);
        if (isExistName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        var existData = await _roleRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);
        try
        {
            await _roleRepository.BeginTransactionAsync();
            await _roleRepository.UpdateAsync(existData);

            var oldPermissionIds = await _permissionRepository
                .FindByCondition(x => !x.IsDeleted && x.RoleId == obj.Id)
                .Select(x => x.Id)
                .ToListAsync();

            await _permissionRepository.SoftDeleteListAsync(oldPermissionIds);

            var permissons = new List<Permission>();

            if (obj.IsCheckAll)
            {
                permissons = await _actionInMenuRepository
                    .FindByCondition(x => !x.IsDeleted)
                    .Select(x => new Permission
                    {
                        CreatedBy = obj.UpdatedBy,
                        CreatedDate = DateTime.Now,
                        ActionId = x.ActionId,
                        MenuId = x.MenuId,
                        RoleId = existData.Id
                    })
                    .ToListAsync();
            }
            else
            {
                if (obj.Permissions.Any())
                {
                    permissons = obj.Permissions
                    .Select(x => new Permission
                    {
                        ActionId = x.ActionId,
                        MenuId = x.MenuId,
                        RoleId = existData.Id,
                        CreatedBy = existData.UpdatedBy,
                        CreatedDate = DateTime.Now,
                    })
                    .ToList();
                }
            }

            if (permissons.Any())
            {
                await _permissionRepository.CreateListAsync(permissons);
            }

            await _roleRepository.SaveChangesAsync();
            await _roleRepository.EndTransactionAsync();

            //update cache
            if (permissons.Any())
            {
                await _cacheService.RemoveAsync(CommonConstants.Cache.PERMISSIONS_ALL_KEY);
                var permissions = await _permissionRepository
                    .GetAllAsync();
                await _cacheService.SetAsync<List<Permission>>(CommonConstants.Cache.PERMISSIONS_ALL_KEY, permissions);
            }
        }
        catch
        {
            await _roleRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();

        }


        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateRoleDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdatePermissionAsync(int roleId, List<UpdatePermissionDto> objs)
    {
        var existPermissionIds = await _permissionRepository
            .FindByCondition(x => !x.IsDeleted && x.RoleId == roleId)
            .Select(x => x.Id)
            .ToListAsync();

        await _permissionRepository.SoftDeleteListAsync(existPermissionIds);

        var model = objs.Select(x => x.ToEntity()).ToList();

        await _permissionRepository.CreateListAsync(model);

        await _permissionRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public async Task<ApiResponse> GetByAdminRegister()
    {
        var data = await _roleRepository
            .FindByCondition(x => !x.IsDeleted && CommonConstants.ListRoleRegister.Contains(x.Id))
            .Select(x => new RoleDetailDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                Description = x.Description,
                Name = x.Name
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetListRoleForUserManagementAsync()
    {
        var data = await _roleRepository
           .FindByCondition(x => x.Id != CommonConstants.Role.DRIVER)
           .Select(x => new RoleDetailDto
           {
               Id = x.Id,
               CreatedDate = x.CreatedDate,
               Description = x.Description,
               Name = x.Name
           })
           .ToListAsync();

        return ApiResponse.Success(data);
    }
}
