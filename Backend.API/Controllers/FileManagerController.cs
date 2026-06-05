using Asp.Versioning;
using Backend.API.Utilities;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.FolderUploads;
using Backend.Application.Interfaces;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Backend.API.Controllers
{
    [Authorize]
    [ApiVersion(1)]
    [Route("api/v{version:apiVersion}/file-manager")]
    [ApiController]
    public class FileManagerController : BaseController
    {
        public readonly IFileUploadService _fileUploadService;
        public readonly IStorageService _storageService;

        public FileManagerController(IFileUploadService fileUploadService, IStorageService storageService)
        {
            _fileUploadService = fileUploadService;
            _storageService = storageService;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload([FromForm] UploadFileRequest request)
        {
            request.CreatedBy = this.GetLoggedInUserId();
            var result = await _fileUploadService.UploadAsync(request);

            return BaseResult(result);
        }

        [HttpPost("folders")]
        public async Task<IActionResult> CreateFolderAsync([FromBody] CreateFolderUploadDto obj)
        {
            obj.CreatedBy = this.GetLoggedInUserId();
            obj.ParentId = obj.ParentId != 0 ? obj.ParentId : null;
            var result = await _fileUploadService.CreateFolderAsync(obj);

            return BaseResult(result);
        }

        [HttpGet("folders")]
        public async Task<IActionResult> GetAllFoldersAsync()
        {
            var result = await _fileUploadService.GetFoldersAsync();

            return BaseResult(result);
        }

        [HttpGet("folders/sub-folder")]
        public async Task<IActionResult> GetFoldersAsync(int? parentId)
        {
            var result = await _fileUploadService.GetSubFoldersAsync(parentId);

            return BaseResult(result);
        }

        [HttpPost("upload-by-category")]
        public async Task<IActionResult> UploadFileByCategory([FromForm] UploadFileByCategory request)
        {
            request.CreatedBy = this.GetLoggedInUserId();
            var result = await _fileUploadService.UploadByCategoryAsync(request);

            return BaseResult(result);
        }

        [HttpPost("folders/{folderId}/paged")]
        public async Task<IActionResult> GetPagedAsync(int folderId, [FromBody] FileManagerFilter query)
        {
            var result = await _fileUploadService.GetPagedAsync(folderId, query);

            return BaseResult(result);
        }

        [HttpPost("{category}/paged")]
        public async Task<IActionResult> GetPagedAsync(string category, [FromBody] FileManagerFilter query)
        {
            var result = await _fileUploadService.GetPagedAsync(category, query);

            return BaseResult(result);
        }

        [HttpGet("{category}/allowed-extentions")]
        public async Task<IActionResult> GetAllowedExtensionsByCategoryAsync(string category)
        {
            var result = await _fileUploadService.GetAllowedExtensionsByCategoryAsync(category);

            return BaseResult(result);
        }

        [HttpGet("view")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTempFile(string path, long expires, string token)
        {
            if (!_storageService.ValidateTemporaryAccess(path, expires, token))
                return Unauthorized(ApiResponse.Unauthorized("Liên kết không hợp lệ hoặc đã hết hạn.", null));

            var fullPath = _storageService.GetPublicUrl(path);
            if (string.IsNullOrEmpty(fullPath))
                return NotFound(ApiResponse.NotFound("Tệp tin không tồn tại hoặc đã bị xoá.", null));
            await Task.CompletedTask;

            var ext = FileHelper.GetFileExtension(fullPath);
            var mimeType = FileHelper.GetMimeType(ext) ?? "application/octet-stream";

            var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
            if (FileHelper.IsImage(ext) || FileHelper.IsVideo(ext) || FileHelper.IsAudio(ext) || ext == ".pdf")
                return File(stream, mimeType);

            return File(stream, mimeType, Path.GetFileName(fullPath));
        }
    }
}
