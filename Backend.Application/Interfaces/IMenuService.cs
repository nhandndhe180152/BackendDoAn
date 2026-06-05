using System;
using Backend.Application.DTOs.Menus;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IMenuService : IServiceBase<int, CreateMenuDto, UpdateMenuDto, DTParameter>
{
    Task<ApiResponse> GetMenuPermissionAsync();
    Task<ApiResponse> GetAllMenuTypeAsync();
}
