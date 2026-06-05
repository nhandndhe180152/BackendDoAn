using System;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.FolderUploads;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IFileUploadService : IServiceBase<int, CreateFileUploadDto, UpdateFileUploadDto, DTParameter>
{
    Task<ApiResponse> UploadAsync(UploadFileRequest request);
    Task<ApiResponse> CreateFolderAsync(CreateFolderUploadDto obj);
    Task<ApiResponse> DeleteFolderAsync(int id);
    Task<ApiResponse> GetPagedAsync(string category, FileManagerFilter query);
    Task<ApiResponse> GetPagedAsync(int folderId, FileManagerFilter query);
    Task<ApiResponse> GetFoldersAsync();
    Task<ApiResponse> GetSubFoldersAsync(int? parentId);
    Task<ApiResponse> UploadByCategoryAsync(UploadFileByCategory request);
    Task<ApiResponse> GetAllowedExtensionsByCategoryAsync(string category);
}
