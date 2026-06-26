using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
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

public class ProductCategoryRepository : RepositoryBase<ProductCategory, int>, IProductCategoryRepository
{
    private readonly BackendContext _context;

    public ProductCategoryRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<ProductCategoryAggregate>> GetPagedAsync(ProductCategoryDTParameters parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "Id";
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.ProductCategories
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new ProductCategoryAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Description = x.Description,
                ParentId = x.ParentCategoryId,
                ParentName = x.ParentCategory != null ? x.ParentCategory.Name : null,
                TreeIds = x.TreeIds,
                SortOrder = x.SortOrder,
                CreatedDate = x.CreatedDate,
                ProductCount = x.Products.Count(p => !p.IsDeleted)
            });

        var totalRecord = await query.CountAsync();

        // Tìm kiếm chung theo tên / mô tả
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)));
        }

        // Lọc theo từng cột (đồng bộ với SupplierRepository)
        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "name":
                    case "Name":
                        query = query.Where(x => x.Name.Contains(search));
                        break;
                    case "description":
                    case "Description":
                        query = query.Where(x => x.Description != null && x.Description.Contains(search));
                        break;
                    case "parentName":
                    case "ParentName":
                        // Frontend gửi Id danh mục cha -> lọc theo ParentId; nếu là chuỗi thì lọc theo tên
                        if (int.TryParse(search, out var parentId))
                            query = query.Where(x => x.ParentId == parentId);
                        else
                            query = query.Where(x => x.ParentName != null && x.ParentName.Contains(search));
                        break;
                    case "sortOrder":
                    case "SortOrder":
                        if (int.TryParse(search, out var sortOrder))
                            query = query.Where(x => x.SortOrder == sortOrder);
                        break;
                    case "createdDate":
                    case "CreatedDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.CreatedDate >= startDate && x.CreatedDate <= endDate);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        {
                            query = query.Where(x => x.CreatedDate.Date == date.Date);
                        }
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return new DTResult<ProductCategoryAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName)
    {
        return columnName switch
        {
            "id" => "Id",
            "name" => "Name",
            "description" => "Description",
            "parentName" => "ParentName",
            "sortOrder" => "SortOrder",
            "productCount" => "ProductCount",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}
