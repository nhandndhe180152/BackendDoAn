using System;
using Backend.Application.DTOs.Users;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserService : IServiceBase<int, CreateUserDto, UpdateUserDto, UserDTParameters>
{
    Task<ApiResponse> GetMenuAsync(int userId);
    Task<ApiResponse> GetProfileAsync(int userId);
    Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
    Task<ApiResponse> UpdateProfileAsync(int userId, UpdateUserProfileDto updateUserProfileDto);
    Task<ApiResponse> GetPermissionsAsync(int userId);
    Task<ApiResponse> GetPagedEndUserAsync(SearchQuery query);
    Task<ApiResponse> Deactivate(int userId);
    Task<ApiResponse> GetAllAsync(UserSearchQuery query);
}
