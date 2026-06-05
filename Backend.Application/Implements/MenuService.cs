using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.Menus;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Enums;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class MenuService : IMenuService
{
    private readonly IMenuRepository _menuRepository;
    private readonly IActionInMenuRepository _actionInMenuRepository;
    private readonly IPermissionRepository _permissionRepository;
    private readonly IActionRepository _actionRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    public MenuService(IMenuRepository menuRepository, IActionInMenuRepository actionInMenuRepository, IPermissionRepository permissionRepository, IActionRepository actionRepository, IHttpContextAccessor httpContextAccessor)
    {
        _menuRepository = menuRepository;
        _actionInMenuRepository = actionInMenuRepository;
        _permissionRepository = permissionRepository;
        _actionRepository = actionRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> CreateAsync(CreateMenuDto obj)
    {
        var exist = await _menuRepository
            .AnyAsync(x => !x.IsDeleted && x.Name.ToLower() == obj.Name.ToLower() && x.MenuType == obj.MenuType);
        if (exist)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData);

        var treeIds = string.Empty;
        if (obj.ParentId.HasValue && obj.ParentId.Value != 0)
        {
            var parentMenu = await _menuRepository
                .FindByCondition(x => x.Id == obj.ParentId && !x.IsDeleted)
                .Select(x => new
                {
                    x.TreeIds,
                    x.Id
                })
                .FirstOrDefaultAsync();
            if (parentMenu == null)
                return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.UnprocessableEntity), ApiCodeConstants.Common.UnprocessableEntity);
            treeIds = parentMenu.TreeIds;
        }
        var model = obj.ToEntity();
        await _menuRepository.BeginTransactionAsync();
        try
        {
            await _menuRepository.CreateAsync(model);
            await _menuRepository.SaveChangesAsync();

            model.TreeIds = string.IsNullOrEmpty(treeIds) ? model.Id.ToString() : $"{treeIds}_{model.Id}";
            await _menuRepository.UpdateAsync(model);

            var actionInMenus = obj.ActionIds
                .Select(x => new ActionInMenu
                {
                    ActionId = x,
                    MenuId = model.Id,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = DateTime.Now
                })
                .ToList();
            await _actionInMenuRepository.CreateListAsync(actionInMenus);
            await _actionInMenuRepository.SaveChangesAsync();


            await _menuRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            await _menuRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Success(model.Id);
    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateMenuDto> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _menuRepository
            .FindByCondition(x => !x.IsDeleted)
            .OrderBy(x => x.TreeIds)
            .ThenBy(x => x.SortOrder)
            .Select(x => new MenuDetailDto
            {
                ClassName = x.ClassName,
                Icon = x.Icon,
                Id = x.Id,
                MenuType = x.MenuType,
                Name = x.Name,
                SortOrder = x.SortOrder,
                TreeIds = x.TreeIds,
                Url = x.Url,
                ParentId = x.ParentId
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _menuRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Select(x => new MenuDetailDto
            {
                ParentId = x.ParentId,
                ClassName = x.ClassName,
                Icon = x.Icon,
                Id = x.Id,
                MenuType = x.MenuType,
                Name = x.Name,
                SortOrder = x.SortOrder,
                TreeIds = x.TreeIds,
                Url = x.Url,
                CreatedDate = x.CreatedDate,
                ActionIds = x.ActionInMenus
                    .Where(x => !x.IsDeleted)
                    .Select(x => x.ActionId)
                    .ToList(),
                IsAdminOnly = x.IsAdminOnly
            })
            .FirstOrDefaultAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = await _menuRepository
            .FindByCondition(x => !x.IsDeleted && (string.IsNullOrEmpty(query.Keyword) || x.MenuType == query.Keyword))
            .Select(x => new MenuListDto
            {
                ClassName = x.ClassName,
                Icon = x.Icon,
                Id = x.Id,
                MenuType = x.MenuType,
                Name = x.Name,
                SortOrder = x.SortOrder,
                Url = x.Url,
                ParentId = x.ParentId,
            })
            .ToListAsync();
        var menuTree = BuildMenuTree(data);

        return ApiResponse.Success(menuTree);

    }

    private List<MenuListDto> BuildMenuTree(List<MenuListDto> menus, int? parentId = null)
    {
        return menus
            .Where(m => m.ParentId == parentId)
            .OrderBy(m => m.SortOrder)
            .Select(m => new MenuListDto
            {
                Id = m.Id,
                Name = m.Name,
                Url = m.Url,
                Icon = m.Icon,
                ClassName = m.ClassName,
                SortOrder = m.SortOrder,
                MenuType = m.MenuType,
                ParentId = m.ParentId,
                Child = BuildMenuTree(menus, m.Id)
            })
            .ToList();
    }


    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _menuRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _menuRepository.SaveChangesAsync();

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateMenuDto obj)
    {
        if (obj.ParentId == obj.Id)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.InvalidData).Replace("{PropertyName}", "Danh mục cha"),
                ApiCodeConstants.Common.InvalidData);

        var existName = await _menuRepository
            .AnyAsync(x => !x.IsDeleted && obj.Id != x.Id && x.Name.ToLower() == obj.Name.ToLower() && x.MenuType == obj.MenuType);
        if (existName)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData);

        var existData = await _menuRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        var treeIds = string.Empty;
        if (obj.ParentId.HasValue)
        {
            var parentMenu = await _menuRepository
                .FindByCondition(x => x.Id == obj.ParentId && !x.IsDeleted)
                .Select(x => new
                {
                    x.TreeIds,
                    x.Id
                })
                .FirstOrDefaultAsync();
            if (parentMenu == null)
                return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.UnprocessableEntity), ApiCodeConstants.Common.UnprocessableEntity);
            treeIds = parentMenu.TreeIds;
        }
        var model = obj.ToEntity(existData);
        var oldTreeIds = model.TreeIds;
        await _menuRepository.BeginTransactionAsync();
        try
        {
            //update menu
            await _menuRepository.UpdateAsync(model);
            model.TreeIds = string.IsNullOrEmpty(treeIds) ? model.Id.ToString() : $"{treeIds}_{model.Id}";
            await _menuRepository.UpdateAsync(model);

            //update tree ids for child menu
            var childMenu = await _menuRepository
            .FindByCondition(x => x.TreeIds.StartsWith(oldTreeIds))
            .ToListAsync();
            if (childMenu.Any())
            {
                childMenu.ForEach(x => x.TreeIds = x.TreeIds.Replace(oldTreeIds, model.TreeIds));
                await _menuRepository.UpdateListAsync(childMenu);
            }

            //Soft delete old action in menu
            var oldActionMenuIds = await _actionInMenuRepository
                .FindByCondition(x => !x.IsDeleted && x.MenuId == model.Id)
                .Select(x => x.Id)
                .ToListAsync();
            await _actionInMenuRepository.SoftDeleteListAsync(oldActionMenuIds);

            //add new action in menu
            var actionInMenus = obj.ActionIds
                .Select(x => new ActionInMenu
                {
                    ActionId = x,
                    MenuId = model.Id,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = DateTime.Now
                })
                .ToList();
            await _actionInMenuRepository.CreateListAsync(actionInMenus);
            await _actionInMenuRepository.SaveChangesAsync();


            await _menuRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            await _menuRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Success(model.Id);
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateMenuDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetMenuPermissionAsync()
    {
        var currentRoleIds = _httpContextAccessor.HttpContext?.GetCurrentRoleIds();
        var isAdmin = currentRoleIds != null && currentRoleIds.Any(x => x == CommonConstants.Role.ADMIN);

        var data = await (from a in _menuRepository.GetAll()
                          join b in _actionInMenuRepository.GetAll() on a.Id equals b.MenuId
                          join c in _actionRepository.GetAll() on b.ActionId equals c.Id into groupAB
                          from ab in groupAB.DefaultIfEmpty()
                          where !a.IsDeleted && !b.IsDeleted && !ab.IsDeleted && a.MenuType == CommonConstants.MenuType.ADMIN
                            && (isAdmin || !a.IsAdminOnly)
                          select new
                          {
                              a.Id,
                              a.Name,
                              a.TreeIds,
                              a.SortOrder,
                              ActionId = ab.Id
                          })
                  .GroupBy(x => new
                  {
                      x.Id,
                      x.TreeIds,
                      x.Name
                  })
                  .Select(x => new MenuPermissionDto
                  {
                      Id = x.Key.Id,
                      Name = x.Key.Name,
                      TreeIds = x.Key.TreeIds,
                      HasCreate = x.Any(xx => xx.ActionId == (int)Enums.Action.CREATE),
                      HasRead = x.Any(xx => xx.ActionId == (int)Enums.Action.READ),
                      HasUpdate = x.Any(xx => xx.ActionId == (int)Enums.Action.UPDATE),
                      HasDelete = x.Any(xx => xx.ActionId == (int)Enums.Action.DELETE),
                      HasExport = x.Any(xx => xx.ActionId == (int)Enums.Action.EXPORT),
                      HasApprove = x.Any(xx => xx.ActionId == (int)Enums.Action.APPROVE)
                  })
                  .OrderBy(x => x.TreeIds)
                  .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetAllMenuTypeAsync()
    {
        var data = CommonConstants.ListMenuType
            .ToList();

        await Task.CompletedTask;

        return ApiResponse.Success(data);
    }
}
