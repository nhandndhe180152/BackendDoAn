using System;
using System.Globalization;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class BlogPostRepository : RepositoryBase<BlogPost, int>, IBlogPostRepository
{
    private readonly BackendContext _context;
    private readonly IStorageService _storageService;
    public BlogPostRepository(BackendContext context, IUnitOfWork unitOfWork, IStorageService storageService) : base(context, unitOfWork)
    {
        _context = context;
        _storageService = storageService;
    }

    public async Task<DTResult<BlogPostAggregate>> GetPagedAsync(BlogPostDTParameters parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null && parameters.Order.Length > 0)
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() != "desc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = _context.BlogPosts
            .Where(x => !x.IsDeleted
                    && !x.Author.IsDeleted
                    && !x.BlogPostLayout.IsDeleted
                    && !x.BlogPostStatus.IsDeleted
                    && !x.BlogCategory.IsDeleted
            ).Select(x => new BlogPostAggregate
            {
                Id = x.Id,
                BlogPostCategoryId = x.BlogPostCategoryId,
                BlogPostCategoryName = x.BlogCategory.Name,
                BlogPostCategoryColor = x.BlogCategory.Color,
                BlogLayoutId = x.BlogPostLayoutId,
                BlogPostStatusId = x.BlogPostStatusId,
                BlogPostStatusName = x.BlogPostStatus.Name,
                BlogPostStatusColor = x.BlogPostStatus.Color,
                BlogLayoutName = x.BlogPostLayout.Name,
                AuthorId = x.AuthorId,
                AuthorName = x.Author.FirstName + " " + x.Author.LastName,
                CoverImageId = x.CoverImageId,
                CoverImageUrl = x.CoverImage != null ? _storageService.GetOriginalUrl(x.CoverImage.FileKey) : null,
                Title = x.Title,
                Content = x.Content,
                Description = x.Description,
                SeoAlias = x.SeoAlias,
                ApprovalDate = x.ApprovalDate,
                PublicationDate = x.PublicationDate,
                IsApproved = x.IsApproved,
                IsShowOnHomePage = x.IsShowOnHomePage,
                IsPopular = x.IsPopular,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => EF.Functions.Collate(x.Title, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.BlogPostCategoryName != null && EF.Functions.Collate(x.BlogPostCategoryName, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.BlogPostStatusName != null && EF.Functions.Collate(x.BlogPostStatusName, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.AuthorName != null && EF.Functions.Collate(x.AuthorName, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.BlogLayoutName != null && EF.Functions.Collate(x.BlogLayoutName, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.Content != null && EF.Functions.Collate(x.Content, SQLParams.Latin_General).Contains(keyword)) ||
                    x.CreatedDate.ToVietnameseDateTime().Contains(keyword)
                 );
        }
        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (!search.Contains("/"))
            {
                search = column.Search.Value.ToLower();
            }
            if (string.IsNullOrEmpty(search)) continue;
            switch (column.Data)
            {
                case "title":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Title, SQLParams.Latin_General).Contains(search));
                    break;
                case "description":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Description, SQLParams.Latin_General).Contains(search));
                    break;
                case "createdDate":
                    if (search.Contains(" - "))
                    {
                        var dates = search.Split(" - ");
                        var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                        query = query.Where(c => c.CreatedDate >= startDate && c.CreatedDate <= endDate);
                    }
                    else
                    {
                        var date = DateTime.ParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                        query = query.Where(c => c.CreatedDate.Date == date.Date);
                    }
                    break;
            }
        }

        if (parameters.BlogPostCategoryIds.Count > 0)
        {
            query = query.Where(x => parameters.BlogPostCategoryIds.Contains(x.BlogPostCategoryId));
        }
        if (parameters.BlogPostLayoutIds.Count > 0)
        {
            query = query.Where(x => parameters.BlogPostLayoutIds.Contains(x.BlogLayoutId));
        }
        if (parameters.BlogPostStatusIds.Count > 0)
        {
            query = query.Where(x => parameters.BlogPostStatusIds.Contains(x.BlogPostStatusId));
        }
        if (parameters.AuthorIds.Count > 0)
        {
            query = query.Where(x => parameters.AuthorIds.Contains(x.AuthorId));
        }

        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = new DTResult<BlogPostAggregate>
        {
            draw = parameters.Draw,
            data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }


    public async Task<PagingData<BlogPostAggregate>> GetPagedClientAsync(BlogPostSearchDTParameters parameters)
    {
        var orderCriteria = "Id";
        var orderAscendingDirection = true;

        if (string.Equals(parameters.OrderBy, "ApprovalDate", StringComparison.OrdinalIgnoreCase))
        {
            orderCriteria = "ApprovalDate";
            orderAscendingDirection = !string.Equals(parameters.SortType, "desc", StringComparison.OrdinalIgnoreCase);
        }

        var query = from bp in _context.BlogPosts
                    join author in _context.Users on bp.AuthorId equals author.Id
                    join layout in _context.BlogPostLayouts on bp.BlogPostLayoutId equals layout.Id
                    join status in _context.BlogPostStatuses on bp.BlogPostStatusId equals status.Id
                    join category in _context.BlogPostCategories on bp.BlogPostCategoryId equals category.Id
                    join bt in _context.BlogPostTags on bp.Id equals bt.BlogPostId into blogPostTags
                    from bt in blogPostTags.DefaultIfEmpty()
                    join tag in _context.Tags on bt.TagId equals tag.Id into tags
                    from tag in tags.DefaultIfEmpty()
                    where !bp.IsDeleted
                       && !author.IsDeleted
                       && !layout.IsDeleted
                       && !status.IsDeleted
                       && status.Id == 1002  // Chỉ lấy các bài viết đang hoạt động
                       && bp.IsApproved
                       && !category.IsDeleted
                       && (bt == null || !bt.IsDeleted)
                    group new { bp, author, layout, status, category, bt, tag } by new
                    {
                        bp.Id,
                        bp.BlogPostCategoryId,
                        category.Name,
                        bp.AuthorId,
                        AuthorName = author.FirstName + " " + author.LastName,
                        bp.CoverImageId,
                        bp.Title,
                        bp.Content,
                        bp.Description,
                        bp.SeoAlias,
                        bp.PublicationDate,
                        bp.ApprovalDate,
                        bp.CreatedDate
                    } into g
                    select new BlogPostAggregate
                    {
                        Id = g.Key.Id,
                        BlogPostCategoryId = g.Key.BlogPostCategoryId,
                        BlogPostCategoryName = g.Key.Name,
                        AuthorId = g.Key.AuthorId,
                        AuthorName = g.Key.AuthorName,
                        CoverImageId = g.Key.CoverImageId,
                        CoverImageUrl = g.First().bp.CoverImage != null ? _storageService.GetOriginalUrl(g.First().bp.CoverImage.FileKey) : null,
                        Title = g.Key.Title,
                        Content = g.Key.Content,
                        Description = g.Key.Description,
                        SeoAlias = g.Key.SeoAlias,
                        PublicationDate = g.Key.PublicationDate,
                        ApprovalDate = g.Key.ApprovalDate,
                        CreatedDate = g.Key.CreatedDate,
                        BlogPostTagIds = g.Where(x => x.bt != null && x.tag != null)
                            .Select(x => new DataItem<int>
                            {
                                Id = x.bt.TagId,
                                Name = x.tag.Name
                            }).ToList()
                    };

        var totalRecord = await query.CountAsync();

        // Tìm kiếm theo từ khóa
        if (!string.IsNullOrEmpty(parameters.Keyword))
        {
            var keyword = parameters.Keyword.ToLower();
            query = query.Where(x =>
                EF.Functions.Collate(x.Title, SQLParams.Latin_General).Contains(keyword) ||
                (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                (x.BlogPostCategoryName != null && EF.Functions.Collate(x.BlogPostCategoryName, SQLParams.Latin_General).Contains(keyword)) ||
                (x.AuthorName != null && EF.Functions.Collate(x.AuthorName, SQLParams.Latin_General).Contains(keyword)) ||
                (x.Content != null && EF.Functions.Collate(x.Content, SQLParams.Latin_General).Contains(keyword)) ||
                x.BlogPostTagIds.Any(t => EF.Functions.Collate(t.Name, SQLParams.Latin_General).Contains(keyword)) ||
                x.CreatedDate.ToVietnameseDateTime().Contains(keyword)

            );
        }

        // Lọc theo danh mục
        if (parameters.BlogPostCategoryId.HasValue)
        {
            query = query.Where(x => x.BlogPostCategoryId == parameters.BlogPostCategoryId.Value);
        }

        // Lọc theo tag
        if (parameters.BlogPostTagIds.Count > 0)
        {
            query = query.Where(x => x.BlogPostTagIds.Any(t => parameters.BlogPostTagIds.Contains(t.Id)));
        }

        // Sắp xếp
        query = query.OrderByDynamic(orderCriteria, orderAscendingDirection ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);

        // Phân trang
        var pagedData = new PagingData<BlogPostAggregate>
        {
            CurrentPage = parameters.PageIndex,
            PageSize = parameters.PageSize,
            DataSource = await query
                .Skip((parameters.PageIndex - 1) * parameters.PageSize)
                .Take(parameters.PageSize)
                .ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await query.CountAsync()
        };

        return pagedData;
    }
}
