using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.BlogPosts;
using Backend.Application.DTOs.FileUploads;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class BlogPostService : IBlogPostService
{
    private readonly IBlogPostRepository _blogPostRepository;
    private readonly IBlogTagRepository _blogTagRepository;
    private readonly IStorageService _storageService;
    private readonly ILogger<BlogPostService> _logger;

    public BlogPostService(IBlogPostRepository blogPostRepository,
         IBlogTagRepository blogTagRepository,
         IStorageService storageService,
         ILoggerFactory loggerFactory)
    {
        _blogPostRepository = blogPostRepository;
        _blogTagRepository = blogTagRepository;
        _storageService = storageService;
        _logger = loggerFactory.CreateLogger<BlogPostService>();
    }

    public async Task<ApiResponse> CreateAsync(CreateBlogPostDto obj)
    {
        var existData = await _blogPostRepository
            .AnyAsync(x => x.Title.ToLower() == obj.Title.ToLower() && x.BlogPostCategoryId == obj.BlogPostCategoryId);
        if (existData)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Title),
                ApiCodeConstants.Common.DuplicatedData);
        }

        var model = obj.ToEntity();
        try
        {
            await _blogPostRepository.BeginTransactionAsync();

            await _blogPostRepository.CreateAsync(model);
            await _blogPostRepository.SaveChangesAsync();

            if (obj.BlogPostTagIds.Any())
            {
                var objs = obj.BlogPostTagIds.Select(x => new BlogPostTag
                {
                    BlogPostId = model.Id,
                    TagId = x,
                    CreatedBy = obj.CreatedBy,
                    CreatedDate = DateTime.Now,
                });
                await _blogTagRepository.CreateListAsync(objs);
                await _blogTagRepository.SaveChangesAsync();
            }

            await _blogPostRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create blog post with message {Message}", ex.Message);
            await _blogPostRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Created(model.Id);
    }

    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateBlogPostDto> objs)
    {
        var model = objs.Select(x => x.ToEntity());

        await _blogPostRepository.CreateListAsync(model);
        await _blogPostRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Select(x => x.Id));
    }

    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _blogPostRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => new BlogPostListDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                //Content = x.Content,
                ApprovalDate = x.ApprovalDate,
                AuthorId = x.AuthorId,
                AuthorName = x.Author.FirstName + " " + x.Author.LastName,
                BlogPostCategoryId = x.BlogPostCategoryId,
                BlogPostCategoryName = x.BlogCategory.Name,
                BlogPostLayoutId = x.BlogPostLayoutId,
                BlogPostLayoutName = x.BlogPostLayout.Name,
                BlogPostStatusId = x.BlogPostStatusId,
                BlogPostStatusName = x.BlogPostStatus.Name,
                CoverImageId = x.CoverImageId,
                IsApproved = x.IsApproved,
                IsPopular = x.IsPopular,
                IsShowOnHomePage = x.IsShowOnHomePage,
                PublicationDate = x.PublicationDate,
                SeoAlias = x.SeoAlias,
            })
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _blogPostRepository
                .FindByCondition(x => x.Id == id)
            .Select(x => new BlogPostDetailDto()
            {
                Id = x.Id,
                Title = x.Title,
                SeoAlias = x.SeoAlias,
                Description = x.Description,
                Content = x.Content,
                IsApproved = x.IsApproved,
                ApprovalDate = x.ApprovalDate,
                PublicationDate = x.PublicationDate,
                IsShowOnHomePage = x.IsShowOnHomePage,
                IsPopular = x.IsPopular,
                CoverImage = x.CoverImage != null ? new FileUploadDetailDto
                {
                    Id = x.CoverImage.Id,
                    FileKey = x.CoverImage.FileKey,
                    FileName = x.CoverImage.FileName,
                    FileType = x.CoverImage.FileType,
                    FileSize = x.CoverImage.FileSize,
                } : null,
                BlogCategory = new DataItem<int>
                {
                    Id = x.BlogPostCategoryId,
                    Name = x.BlogCategory.Name
                },
                BlogPostLayout = new DataItem<int>
                {
                    Id = x.BlogPostLayoutId,
                    Name = x.BlogPostLayout.Name
                },
                Author = new DataItem<int>
                {
                    Id = x.AuthorId,
                    Name = x.Author.FirstName + " " + x.Author.LastName
                },
                BlogPostStatus = new DetailStatusDto<int>
                {
                    Id = x.BlogPostStatusId,
                    Name = x.BlogPostStatus.Name,
                    Color = x.BlogPostStatus.Color,
                },
                BlogPostTagIds = x.BlogTags
                    .Where(bt => !bt.IsDeleted)
                    .Select(bt => new DataItem<int>
                    {
                        Id = bt.TagId,
                        Name = bt.Tag.Name
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();
        if (data.CoverImage != null && !string.IsNullOrEmpty(data.CoverImage.FileKey))
            data.CoverImage.FileKey = _storageService.GetOriginalUrl(data.CoverImage.FileKey);

        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var currentDate = DateTime.Now;
        var data = _blogPostRepository
            .FindByCondition(x => x.ApprovalDate.HasValue && x.IsApproved &&
                (x.PublicationDate == null || x.PublicationDate >= currentDate) && x.BlogPostStatusId == CommonConstants.BlogPostStatus.PUBLISHED)
            .Select(x => new BlogPostListDto
            {
                Id = x.Id,
                BlogPostCategoryId = x.BlogPostCategoryId,
                BlogPostCategoryName = x.BlogCategory.Name,
                BlogPostLayoutId = x.BlogPostLayoutId,
                BlogPostLayoutName = x.BlogPostLayout.Name,
                AuthorId = x.AuthorId,
                AuthorName = x.Author.FirstName + " " + x.Author.LastName,
                BlogPostStatusId = x.BlogPostStatusId,
                BlogPostStatusName = x.BlogPostStatus.Name,
                BlogPostStatusColor = x.BlogPostStatus.Color,
                CoverImageId = x.CoverImageId,
                CoverImageUrl = x.CoverImage != null ? _storageService.GetOriginalUrl(x.CoverImage.FileKey) : null,
                Title = x.Title,
                //Content = x.Content,
                SeoAlias = x.SeoAlias,
                Description = x.Description,
                IsApproved = x.IsApproved,
                ApprovalDate = x.ApprovalDate,
                PublicationDate = x.PublicationDate,
                IsShowOnHomePage = x.IsShowOnHomePage,
                IsPopular = x.IsPopular,
            });

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data
                .Where(x => x.Title.ToLower().Contains(query.Keyword.ToLower()) ||
                x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                x.BlogPostCategoryName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.BlogPostLayoutName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.AuthorName.ToLower().Contains(query.Keyword.ToLower()) ||
                x.BlogPostStatusName.ToLower().Contains(query.Keyword.ToLower())
            //x.Content != null && x.Content.ToLower().Contains(query.Keyword.ToLower())
            );

        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data
                .OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }
        else
        {
            data = data
                .OrderByDescending(x => x.PublicationDate);
        }

        var pagedData = new PagingData<BlogPostListDto>
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

    public async Task<ApiResponse> GetPagedAsync(BlogPostDTParameters parameters)
    {
        var data = await _blogPostRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _blogPostRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _blogPostRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> UpdateAsync(UpdateBlogPostDto obj)
    {
        var isExistData = await _blogPostRepository
            .AnyAsync(x => x.Id != obj.Id && x.Title.ToLower() == obj.Title.ToLower() && x.BlogPostCategoryId == obj.BlogPostCategoryId);
        if (isExistData)
        {
            return ApiResponse.UnprocessableEntity(ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Title),
                ApiCodeConstants.Common.DuplicatedData);
        }

        var existData = await _blogPostRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == obj.Id)
            .FirstOrDefaultAsync();
        if (existData == null)
            return ApiResponse.NotFound();

        obj.ToEntity(existData);

        try
        {
            await _blogPostRepository.BeginTransactionAsync();

            await _blogPostRepository.UpdateAsync(existData);

            var oldObjs = await _blogTagRepository
                .FindByCondition(x => x.BlogPostId == obj.Id)
                .Select(x => x.Id)
                .ToListAsync();
            await _blogTagRepository.SoftDeleteListAsync(oldObjs);

            var objs = obj.BlogPostTagIds.Select(x => new BlogPostTag
            {
                TagId = x,
                BlogPostId = obj.Id,
                CreatedBy = obj.UpdatedBy,
                CreatedDate = DateTime.Now
            });
            await _blogTagRepository.CreateListAsync(objs);

            await _blogPostRepository.SaveChangesAsync();
            await _blogPostRepository.EndTransactionAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update blog post with message {Message}", ex.Message);
            await _blogPostRepository.RollbackTransactionAsync();

            return ApiResponse.InternalServerError();
        }

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateBlogPostDto> obj)
    {
        throw new NotImplementedException();
    }

    public async Task<ApiResponse> GetPagedClientAsync(BlogPostSearchDTParameters parameters)
    {
        var data = await _blogPostRepository.GetPagedClientAsync(parameters);
        return ApiResponse.Success(data);
    }

    public async Task<ApiResponse> GetLatestArticleList()
    {
        //Lấy bài viết nổi bật
        var featuredPost = await _blogPostRepository
            .FindByCondition(x =>
            x.IsApproved &&
            x.IsPopular &&
            x.PublicationDate != null &&
            x.PublicationDate > DateTime.Now)
            .OrderByDescending(x => x.PublicationDate)
            .Select(x => new BlogPostListEndUserDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                PublicationDate = x.PublicationDate,
                SeoAlias = x.SeoAlias,
                CoverImage = x.CoverImage != null ? new FileUploadDetailDto
                {
                    Id = x.CoverImage.Id,
                    FileKey = x.CoverImage.FileKey,
                    FileName = x.CoverImage.FileName,
                    FileType = x.CoverImage.FileType,
                    FileSize = x.CoverImage.FileSize,
                    Url = _storageService.GetOriginalUrl(x.CoverImage.FileKey),
                } : null,
            }).FirstOrDefaultAsync();

        //Lấy 3 bài viết mới nhất
        var latest3Posts = await _blogPostRepository
            .FindByCondition(x =>
            x.IsApproved &&
            x.IsShowOnHomePage &&
            x.PublicationDate != null &&
            x.PublicationDate > DateTime.Now &&
            (featuredPost == null || x.Id != featuredPost.Id))
            .OrderByDescending(x => x.PublicationDate)
            .Take(3)
            .Select(x => new BlogPostListEndUserDto
            {
                Id = x.Id,
                Title = x.Title,
                Description = x.Description,
                PublicationDate = x.PublicationDate,
                SeoAlias = x.SeoAlias,
                CoverImage = x.CoverImage != null ? new FileUploadDetailDto
                {
                    Id = x.CoverImage.Id,
                    FileKey = x.CoverImage.FileKey,
                    FileName = x.CoverImage.FileName,
                    FileType = x.CoverImage.FileType,
                    FileSize = x.CoverImage.FileSize,
                    Url = _storageService.GetOriginalUrl(x.CoverImage.FileKey),
                } : null,
            }).ToListAsync();

        var data = new List<BlogPostListEndUserDto>();
        if (featuredPost != null)
        {
            data.Add(featuredPost);
        }

        data.AddRange(latest3Posts);

        return ApiResponse.Success(data);
    }
}
