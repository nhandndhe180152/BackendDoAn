using System;
using System.IO;
using Backend.Application.DTOs.Users;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IUserService : IServiceBase<int, CreateUserDto, UpdateUserDto, UserDTParameters>
{
    /// <summary>Sinh file mẫu (xlsx/csv) để import tạo user hàng loạt.</summary>
    Task<(byte[] Content, string ContentType, string FileName)> GenerateImportTemplateAsync(string format);

    /// <summary>Đọc file Excel/CSV upload thành danh sách dòng user (chưa gán vai trò).</summary>
    Task<ApiResponse> ParseImportFileAsync(Stream fileStream, string fileName);

    Task<ApiResponse> GetMenuAsync(int userId);
    Task<ApiResponse> GetProfileAsync(int userId);
    Task<ApiResponse> ChangePasswordAsync(int userId, ChangePasswordDto changePasswordDto);
    Task<ApiResponse> UpdateProfileAsync(int userId, UpdateUserProfileDto updateUserProfileDto);
    Task<ApiResponse> GetPermissionsAsync(int userId);
    Task<ApiResponse> GetStatisticsAsync();
    Task<ApiResponse> GetPagedEndUserAsync(SearchQuery query);
    Task<ApiResponse> Deactivate(int userId);
    Task<ApiResponse> GetAllAsync(UserSearchQuery query);
}
