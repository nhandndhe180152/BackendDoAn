using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.DTOs.FolderUploads;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class FileUploadService : IFileUploadService
{
    private readonly IFileUploadRepository _fileUploadRepository;
    private readonly IFolderUploadRepository _folderUploadRepository;
    private readonly IStorageService _storageService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger _logger;
    public FileUploadService(IFileUploadRepository fileUploadRepository, IStorageService storageService, IFolderUploadRepository folderUploadRepository, IHttpContextAccessor httpContextAccessor, ILoggerFactory loggerFactory)
    {
        _fileUploadRepository = fileUploadRepository;
        _storageService = storageService;
        _folderUploadRepository = folderUploadRepository;
        _httpContextAccessor = httpContextAccessor;
        _logger = loggerFactory.CreateLogger<FileUploadService>();
    }

    public Task<ApiResponse> CreateAsync(CreateFileUploadDto obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> CreateFolderAsync(CreateFolderUploadDto obj)
    {
        if (!FileHelper.IsValidFolderName(obj.FolderName))
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.InvalidFolderName), ApiCodeConstants.FileManager.InvalidFolderName);

        var exist = await _folderUploadRepository
            .AnyAsync(x => x.FolderName == obj.FolderName && obj.ParentId == x.ParentId);
        if (exist)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.FolderName),
                ApiCodeConstants.Common.DuplicatedData);

        var treeIds = string.Empty;
        var parentFolderPath = string.Empty;
        if (obj.ParentId.HasValue)
        {
            var parentFolder = await _folderUploadRepository
                .FindByCondition(x => !x.IsDeleted && x.Id == obj.ParentId)
                .Select(x => new
                {
                    x.Id,
                    x.TreeIds,
                    x.FolderPath
                })
                .FirstOrDefaultAsync();
            if (parentFolder == null)
                return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ParentFolderNotFound), ApiCodeConstants.FileManager.ParentFolderNotFound);
            treeIds = parentFolder.TreeIds;
            parentFolderPath = parentFolder.FolderPath;
        }

        var createFolderRequest = new CreateFolderRequest
        {
            FolderPath = string.IsNullOrEmpty(parentFolderPath) ? obj.FolderName : $"{parentFolderPath}/{obj.FolderName}"
        };
        var response = await _storageService.CreateFolderAsync(createFolderRequest);
        if (response != null && response.Success)
        {
            var model = obj.ToEntity();
            await _folderUploadRepository.CreateAsync(model);
            await _folderUploadRepository.SaveChangesAsync();

            model.TreeIds = string.IsNullOrEmpty(treeIds) ? model.Id.ToString() : $"{treeIds}_{model.Id}";
            model.FolderPath = string.IsNullOrEmpty(parentFolderPath) ? obj.FolderName : $"{parentFolderPath}/{obj.FolderName}";
            await _folderUploadRepository.UpdateAsync(model);
            await _folderUploadRepository.SaveChangesAsync();

            return ApiResponse.Success(model.Id);
        }
        else
        {
            return ApiResponse.InternalServerError();
        }


    }

    public Task<ApiResponse> CreateListAsync(IEnumerable<CreateFileUploadDto> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> DeleteFolderAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetAllAsync()
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetByIdAsync(int id)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetFoldersAsync()
    {
        var data = await _folderUploadRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new FolderUploadListDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                FolderName = x.FolderName,
                FolderPath = x.FolderPath,
                ParentId = x.ParentId,
                TreeIds = x.TreeIds,
            })
            .OrderBy(x => x.TreeIds)
            .ToListAsync();

        var folderTree = BuildFolderTree(data);

        return ApiResponse.Success(folderTree);
    }

    private static List<FolderUploadListDto> BuildFolderTree(List<FolderUploadListDto> folders, int? parentId = null)
    {
        return folders
            .Where(x => x.ParentId == parentId)
            .Select(x => new FolderUploadListDto
            {
                Id = x.Id,
                CreatedDate = x.CreatedDate,
                FolderName = x.FolderName,
                FolderPath = x.FolderPath,
                ParentId = x.ParentId,
                TreeIds = x.TreeIds,
                Childs = BuildFolderTree(folders, x.Id),
            })
            .ToList();
    }

    public Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync<T>(AdvancedSearchQuery<T> query)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> GetPagedAsync(DTParameter parameters)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedAsync(int folderId, FileManagerFilter query)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();

        var data = _fileUploadRepository
            .FindByCondition(x => !x.IsDeleted && x.FolderUploadId == folderId && x.CreatedBy == currentUserId)
            .Select(x => new FileUploadDetailDto
            {
                Id = x.Id,
                FileKey = x.FileKey,
                FileName = x.FileName,
                FileSize = x.FileSize,
                FileType = x.FileType,
                Url = _storageService.GetOriginalUrl(x.FileKey)
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.FileName.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (query.FileTypes.Any())
        {
            data = data
                .Where(x => query.FileTypes.Any(type => x.FileType.StartsWith(type)));
        }

        var pagedData = new PagingData<FileUploadDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.OrderByDescending(x => x.Id).Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public Task<ApiResponse> SoftDeleteAsync(int id)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateAsync(UpdateFileUploadDto obj)
    {
        throw new NotImplementedException();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateFileUploadDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UploadAsync(UploadFileRequest request)
    {
        if (request.Files.Count == 0)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.UnprocessableEntity), ApiCodeConstants.Common.UnprocessableEntity);

        if (request.Files.Count > FileManagerConstants.MAXIMUM_TOTAL_FILE)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitTotalFile).Replace("{max}", FileManagerConstants.MAXIMUM_TOTAL_FILE.ToString()), ApiCodeConstants.FileManager.ReachToLimitTotalFile);

        if (request.Files.Sum(x => x.Length) > FileManagerConstants.MAXIMUM_TOTAL_SIZE)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitTotalSize).Replace("{max}", FileManagerConstants.MAXIMUM_TOTAL_SIZE_IN_MB.ToString()), ApiCodeConstants.FileManager.ReachToLimitTotalSize);

        if (request.Files.Any(x => !FileHelper.IsValidFile(x, FileManagerConstants.MAXIMUM_TOTAL_SIZE, out string error)))
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.InvalidFile), ApiCodeConstants.FileManager.InvalidFile);
        }

        var imageFilesInValid = request.Files
            .Where(x => FileHelper.IsImage(x))
            .Any(x => !FileHelper.IsValidFile(x, FileManagerConstants.MAXIMUM_IMAGE_SIZE, out string errorImage));
        if (imageFilesInValid)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitImageSize)
                .Replace("{max}", FileManagerConstants.MAXIMUM_IMAGE_SIZE_IN_MB.ToString()),
                ApiCodeConstants.FileManager.ReachToLimitImageSize);
        }


        var folder = string.Empty;

        var folderUpload = await _folderUploadRepository.GetByIdAsync(request.FolderUploadId);
        if (folderUpload == null)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.FolderNotFound), ApiCodeConstants.FileManager.FolderNotFound);
        folder = folderUpload.FolderPath;

        if (request.Files.Count == 1)
        {
            var file = request.Files[0];
            var response = await _storageService.UploadAsync(file, folder);
            if (response != null && response.Success)
            {

                var model = new FileUpload
                {
                    FileName = file.FileName,
                    CreatedBy = request.CreatedBy,
                    CreatedDate = DateTime.Now,
                    FileSize = response.FileSize,
                    FileType = file.ContentType,
                    FolderUploadId = request.FolderUploadId,
                    FileKey = response.FilePath
                };

                await _fileUploadRepository.CreateAsync(model);
                await _fileUploadRepository.SaveChangesAsync();

                return ApiResponse.Success(new
                {
                    FileName = model.FileName,
                    FileSize = model.FileSize,
                    FileType = model.FileType,
                    FolderUploadId = request.FolderUploadId,
                    FileKey = model.FileKey,
                    Id = model.Id
                });
            }

            return ApiResponse.InternalServerError();
        }
        else
        {
            var response = await _storageService.UploadMultipleAsync(request.Files, folder);
            if (response != null)
            {
                var listFileUpload = new List<FileUpload>();
                //Todo need to add error file
                var fileUploaded = response.Where(x => x.Success).ToList();
                foreach (var item in fileUploaded)
                {
                    var file = request.Files.Find(x => x.FileName == item.FileName);
                    if (file != null)
                    {
                        var model = new FileUpload
                        {
                            CreatedBy = request.CreatedBy,
                            FileName = item.FileName,
                            CreatedDate = DateTime.Now,
                            FileKey = item.FilePath,
                            FileSize = item.FileSize,
                            FileType = file.ContentType,
                            FolderUploadId = request.FolderUploadId
                        };
                        listFileUpload.Add(model);
                    }

                }
                await _fileUploadRepository.CreateListAsync(listFileUpload);
                await _fileUploadRepository.SaveChangesAsync();

                return ApiResponse.Success(listFileUpload.Select(x => new
                {
                    FileName = x.FileName,
                    FileSize = x.FileSize,
                    FileType = x.FileType,
                    FolderUploadId = request.FolderUploadId,
                    FileKey = x.FileKey,
                    Id = x.Id
                }));
            }
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> GetSubFoldersAsync(int? parentId)
    {
        var data = await (from a in _folderUploadRepository.GetAll()
                          join b in _folderUploadRepository.GetAll() on a.Id equals b.ParentId into groupAB
                          from b in groupAB.DefaultIfEmpty()
                          where !a.IsDeleted && (b == null || !b.IsDeleted) && a.ParentId == parentId
                          select new
                          {
                              a.Id,
                              a.ParentId,
                              a.FolderName,
                              a.FolderPath,
                              a.TreeIds,
                              a.CreatedDate
                          })
                   .GroupBy(x => new
                   {
                       x.Id,
                       x.ParentId,
                       x.FolderName,
                       x.FolderPath,
                       x.TreeIds,
                       x.CreatedDate
                   })
                   .Select(x => new FolderUploadDetailDto
                   {
                       Id = x.Key.Id,
                       CreatedDate = x.Key.CreatedDate,
                       FolderName = x.Key.FolderName,
                       FolderPath = x.Key.FolderPath,
                       ParentId = x.Key.ParentId,
                       TreeIds = x.Key.TreeIds,
                       HasChild = x.Count() > 1
                   })
                   .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> UploadByCategoryAsync(UploadFileByCategory request)
    {
        #region Validate
        if (request.Files.Count == 0)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.UnprocessableEntity), ApiCodeConstants.Common.UnprocessableEntity);

        if (request.Files.Count > FileManagerConstants.MAXIMUM_TOTAL_FILE)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitTotalFile).Replace("{max}", FileManagerConstants.MAXIMUM_TOTAL_FILE.ToString()), ApiCodeConstants.FileManager.ReachToLimitTotalFile);

        if (request.Files.Sum(x => x.Length) > FileManagerConstants.MAXIMUM_TOTAL_SIZE)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitTotalSize).Replace("{max}", FileManagerConstants.MAXIMUM_TOTAL_SIZE_IN_MB.ToString()), ApiCodeConstants.FileManager.ReachToLimitTotalSize);

        if (request.Files.Any(x => !FileHelper.IsValidFile(x, FileManagerConstants.MAXIMUM_TOTAL_SIZE, out string error)))
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitTotalSize), ApiCodeConstants.FileManager.ReachToLimitTotalSize);
        }

        var imageFilesInValid = request.Files
            .Where(x => FileHelper.IsImage(x))
            .Any(x => !FileHelper.IsValidFile(x, FileManagerConstants.MAXIMUM_IMAGE_SIZE, out string errorImage));
        if (imageFilesInValid)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.ReachToLimitImageSize)
                .Replace("{max}", FileManagerConstants.MAXIMUM_IMAGE_SIZE_IN_MB.ToString()),
                ApiCodeConstants.FileManager.ReachToLimitImageSize);
        }

        if (request.Files.Any(x => !FileManagerConstants.IsValidFile(x, request.Category)))
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.InvalidFile), ApiCodeConstants.FileManager.InvalidFile);
        #endregion

        #region Check folder upload
        var folderPath = string.Empty;
        var parentFolderUploadId = FileManagerConstants.FileUploadCategory.GetValueOrDefault(request.Category.ToUpper());
        var parentFolderUpload = await _folderUploadRepository.GetByIdAsync(parentFolderUploadId);
        if (parentFolderUpload == null)
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.FileManager.FolderNotFound), ApiCodeConstants.FileManager.FolderNotFound);
        if (request.Category == "PROFILE" || request.Category == "INSURANCE" || (!request.IsAddNew && request.Category == "IDENTITY_CARD"))
            request.DirectionId = request.CreatedBy;

        folderPath = $"{parentFolderUpload.FolderPath}{(request.DirectionId.HasValue && request.DirectionId.Value > 0 ? $"/{request.DirectionId}" : "/temp")}";

        var folderUploadId = await _folderUploadRepository
            .FindByCondition(x => x.FolderPath == folderPath)
            .Select(x => x.Id)
            .FirstOrDefaultAsync();

        if (folderUploadId == 0)
        {
            var folder = new FolderUpload
            {
                CreatedDate = DateTime.Now,
                CreatedBy = request.CreatedBy,
                FolderName = request.DirectionId.HasValue && request.DirectionId.Value > 0 ? $"{request.DirectionId}" : "temp",
                FolderPath = folderPath,
                ParentId = parentFolderUploadId,
                TreeIds = string.Empty,
            };
            await _folderUploadRepository.CreateAsync(folder);
            await _folderUploadRepository.SaveChangesAsync();

            folder.TreeIds = $"{parentFolderUpload.TreeIds}_{folder.Id}";

            await _folderUploadRepository.UpdateAsync(folder);
            await _folderUploadRepository.SaveChangesAsync();

            folderUploadId = folder.Id;
        }
        #endregion

        #region Upload file
        var isPublic = FileManagerConstants.PublicCategory.Contains(request.Category.ToUpper());
        if (request.Files.Count == 1)
        {
            var file = request.Files[0];
            FileUploadResult? response = null;

            response = await _storageService.UploadAsync(file, folderPath);


            if (response != null && response.Success)
            {

                var model = new FileUpload
                {
                    FileName = file.FileName,
                    CreatedBy = request.CreatedBy,
                    CreatedDate = DateTime.Now,
                    FileSize = response.FileSize,
                    FileType = file.ContentType,
                    FolderUploadId = folderUploadId,
                    FileKey = response.FilePath
                };

                await _fileUploadRepository.CreateAsync(model);
                await _fileUploadRepository.SaveChangesAsync();

                return ApiResponse.Success(new
                {
                    FileName = model.FileName,
                    FileSize = model.FileSize,
                    FileType = model.FileType,
                    FolderUploadId = folderUploadId,
                    FileKey = model.FileKey,
                    FileUrl = isPublic ? _storageService.GetOriginalUrl(model.FileKey) : _storageService.GetTemporaryUrl(model.FileKey),
                    Id = model.Id
                });
            }
        }
        else
        {
            var response = await _storageService.UploadMultipleAsync(request.Files, folderPath);
            if (response != null)
            {
                var listFileUpload = new List<FileUpload>();
                //Todo need to add error file
                var fileUploaded = response.Where(x => x.Success).ToList();
                foreach (var item in fileUploaded)
                {
                    var file = request.Files.Find(x => x.FileName == item.FileName);
                    if (file != null)
                    {
                        var model = new FileUpload
                        {
                            CreatedBy = request.CreatedBy,
                            FileName = item.FileName,
                            CreatedDate = DateTime.Now,
                            FileKey = item.FilePath,
                            FileSize = item.FileSize,
                            FileType = file.ContentType,
                            FolderUploadId = folderUploadId
                        };
                        listFileUpload.Add(model);
                    }

                }
                await _fileUploadRepository.CreateListAsync(listFileUpload);
                await _fileUploadRepository.SaveChangesAsync();

                return ApiResponse.Success(listFileUpload.Select(x => new
                {
                    FileName = x.FileName,
                    FileSize = x.FileSize,
                    FileType = x.FileType,
                    FolderUploadId = folderUploadId,
                    FileKey = x.FileKey,
                    FileUrl = isPublic ? _storageService.GetOriginalUrl(x.FileKey) : _storageService.GetTemporaryUrl(x.FileKey),
                    Id = x.Id
                }));
            }
        }
        #endregion

        return ApiResponse.InternalServerError();
    }

    public async Task<ApiResponse> GetPagedAsync(string category, FileManagerFilter query)
    {
        var currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 0;
        var parenFolderId = FileManagerConstants.FileUploadCategory.GetValueOrDefault(category.ToUpper());
        var isPublic = FileManagerConstants.PublicCategory.Contains(category.ToUpper());

        if (category == "PROFILE")
            query.DirectionId = currentUserId;


        var directionIdStr = query.DirectionId?.ToString();
        var folderIds = await _folderUploadRepository
            .FindByCondition(x => x.ParentId == parenFolderId
                && (query.IsGetAll || x.FolderName == directionIdStr))
            .Select(x => x.Id)
            .ToListAsync();

        var data = _fileUploadRepository
            .FindByCondition(x => folderIds.Contains(x.FolderUploadId) && x.CreatedBy == currentUserId)
            .Select(x => new FileUploadDetailDto
            {
                Id = x.Id,
                FileKey = x.FileKey,
                FileName = x.FileName,
                FileSize = x.FileSize,
                FileType = x.FileType,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.FileName.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (query.FileTypes.Any())
        {
            data = data
                .Where(x => query.FileTypes.Any(type => x.FileType.StartsWith(type)));
        }

        var dataSource = await data.OrderByDescending(x => x.Id)
            .Skip((query.PageIndex - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync();

        dataSource.ForEach(x => x.Url = isPublic
            ? _storageService.GetOriginalUrl(x.FileKey)
            : _storageService.GetTemporaryUrl(x.FileKey));

        var pagedData = new PagingData<FileUploadDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = dataSource,
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    public async Task<ApiResponse> GetAllowedExtensionsByCategoryAsync(string category)
    {
        var data = FileManagerConstants.AllowedExtensionsByCategory.GetValueOrDefault(category.ToUpper()) ?? [];
        await Task.CompletedTask;

        return ApiResponse.Success(data);
    }
}
