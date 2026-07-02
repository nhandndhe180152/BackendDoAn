using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductCategories;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ liên quan đến Danh mục sản phẩm (Product Category)
public class ProductCategoryService : IProductCategoryService
{
    private readonly IProductCategoryRepository _productCategoryRepository;

    /// Khởi tạo ProductCategoryService
    public ProductCategoryService(
        IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    // ──────────────────────────────────────────────────────────
    // CREATE
    // ──────────────────────────────────────────────────────────
    /// Tạo mới một danh mục sản phẩm. Tự động tính TreeIds sau khi có Id.
    public async Task<ApiResponse> CreateAsync(CreateProductCategoryDto obj)
    {
        var name = obj.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            return ApiResponse.BadRequest(message: "Tên danh mục không được để trống.");

        // Validate parent exists and not deleted
        if (obj.ParentCategoryId.HasValue)
        {
            var parent = await _productCategoryRepository.GetByIdAsync(obj.ParentCategoryId.Value);
            if (parent == null || parent.IsDeleted)
                return ApiResponse.UnprocessableEntity(
                    "Danh mục cha không tồn tại hoặc đã bị xóa.",
                    ApiCodeConstants.Common.InvalidData);
        }

        // Sibling uniqueness (same parent, same name case-insensitive)
        var hasDupSibling = await _productCategoryRepository.AnyAsync(x =>
            !x.IsDeleted &&
            x.Name.ToLower() == name.ToLower() &&
            x.ParentCategoryId == obj.ParentCategoryId);

        if (hasDupSibling)
            return ApiResponse.Conflict(
                $"Đã tồn tại danh mục '{name}' trong cùng cấp cha.",
                ApiCodeConstants.Common.DuplicatedData);

        // Insert first without TreeIds, then compute & update inside a transaction
        var model = new ProductCategory
        {
            Name = name,
            Description = obj.Description,
            ParentCategoryId = obj.ParentCategoryId,
            TreeIds = string.Empty, // temporary until ID is known
            SortOrder = obj.SortOrder,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };

        await using var tx = await _productCategoryRepository.BeginTransactionAsync();
        try
        {
            await _productCategoryRepository.CreateAsync(model);
            await _productCategoryRepository.SaveChangesAsync(); // flush to get model.Id

            // Compute TreeIds now that we have the Id
            if (obj.ParentCategoryId.HasValue)
            {
                var parent = await _productCategoryRepository.GetByIdAsync(obj.ParentCategoryId.Value);
                model.TreeIds = $"{parent!.TreeIds},{model.Id}";
            }
            else
            {
                model.TreeIds = model.Id.ToString();
            }

            await _productCategoryRepository.UpdateAsync(model);
            await _productCategoryRepository.SaveChangesAsync();

            await _productCategoryRepository.EndTransactionAsync();
        }
        catch
        {
            await _productCategoryRepository.RollbackTransactionAsync();
            throw;
        }

        return ApiResponse.Created(model.Id);
    }

    /// Tạo danh sách nhiều danh mục
    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateProductCategoryDto> objs)
    {
        var models = objs.Select(x => x.ToEntity()).ToList();
        await _productCategoryRepository.CreateListAsync(models);
        await _productCategoryRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    // ──────────────────────────────────────────────────────────
    // READ
    // ──────────────────────────────────────────────────────────
    /// Lấy danh sách toàn bộ danh mục sản phẩm (kèm thông tin danh mục cha)
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ParentCategory)
            .Include(x => x.Products)
            .Select(x => x.ToDto())
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    /// Lấy chi tiết thông tin danh mục theo ID
    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _productCategoryRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.ParentCategory)
            .Include(x => x.Products)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    /// Trả về toàn bộ cây danh mục dạng phân cấp
    public async Task<ApiResponse> GetTreeAsync()
    {
        var all = await _productCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.Products)
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Name)
            .ToListAsync();

        var roots = all
            .Where(x => x.ParentCategoryId == null)
            .Select(x => BuildTree(x, all))
            .ToList();

        return ApiResponse.Success(roots);
    }

    private static ProductCategoryTreeDto BuildTree(ProductCategory node, List<ProductCategory> all)
    {
        var dto = new ProductCategoryTreeDto
        {
            Id = node.Id,
            Name = node.Name,
            Description = node.Description,
            ParentCategoryId = node.ParentCategoryId,
            TreeIds = node.TreeIds,
            SortOrder = node.SortOrder,
            ProductCount = node.Products != null ? node.Products.Count(p => !p.IsDeleted) : 0,
            Children = all
                .Where(c => c.ParentCategoryId == node.Id)
                .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
                .Select(c => BuildTree(c, all))
                .ToList()
        };
        return dto;
    }

    /// Phân trang danh mục sản phẩm theo từ khóa
    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _productCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ParentCategory)
            .Include(x => x.Products)
            .Select(x => x.ToDto());

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data.OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<ProductCategoryDetailDto>
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

    /// Phân trang nâng cao khớp bộ lọc DataTable
    public async Task<ApiResponse> GetPagedAsync(ProductCategoryDTParameters parameters)
    {
        var data = await _productCategoryRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    /// Lọc phân trang chi tiết theo ParentId
    public async Task<ApiResponse> GetPagedAsync(ProductCategorySearchQuery query)
    {
        var data = _productCategoryRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ParentCategory)
            .Include(x => x.Products)
            .Select(x => x.ToDto());

        var totalRecord = await data.CountAsync();
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()));
        }

        // Lọc theo Id của danh mục cha
        if (query.ParentId.HasValue)
        {
            data = data.Where(x => x.ParentCategoryId == query.ParentId.Value);
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data.OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<ProductCategoryDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = await data.CountAsync()
        };

        return ApiResponse.Success(pagedData);
    }

    // ──────────────────────────────────────────────────────────
    // UPDATE
    // ──────────────────────────────────────────────────────────
    /// Cập nhật thông tin danh mục sản phẩm (bao gồm re-parent với cascade TreeIds)
    public async Task<ApiResponse> UpdateAsync(UpdateProductCategoryDto obj)
    {
        var existData = await _productCategoryRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // ── Rule 1: Self-parent ──
        if (obj.ParentCategoryId.HasValue && obj.ParentCategoryId.Value == obj.Id)
            return ApiResponse.UnprocessableEntity(
                "Danh mục không thể là danh mục cha của chính nó.",
                ApiCodeConstants.Common.InvalidData);

        // ── Rule 2: Ancestor loop — cannot move under own descendant ──
        if (obj.ParentCategoryId.HasValue)
        {
            var descendantPrefix = existData.TreeIds + ",";
            var isDescendant = await _productCategoryRepository.AnyAsync(x =>
                !x.IsDeleted &&
                x.Id == obj.ParentCategoryId.Value &&
                (x.TreeIds == existData.TreeIds || x.TreeIds.StartsWith(descendantPrefix)));

            if (isDescendant)
                return ApiResponse.UnprocessableEntity(
                    "Không thể chuyển danh mục vào bên dưới một trong các danh mục con của nó.",
                    ApiCodeConstants.Common.InvalidData);

            // Validate new parent exists and not deleted
            var newParent = await _productCategoryRepository.GetByIdAsync(obj.ParentCategoryId.Value);
            if (newParent == null || newParent.IsDeleted)
                return ApiResponse.UnprocessableEntity(
                    "Danh mục cha không tồn tại hoặc đã bị xóa.",
                    ApiCodeConstants.Common.InvalidData);
        }

        // ── Rule 3: Sibling uniqueness ──
        var hasDupSibling = await _productCategoryRepository.AnyAsync(x =>
            !x.IsDeleted &&
            x.Id != obj.Id &&
            x.Name.ToLower() == obj.Name.Trim().ToLower() &&
            x.ParentCategoryId == obj.ParentCategoryId);

        if (hasDupSibling)
            return ApiResponse.Conflict(
                $"Đã tồn tại danh mục '{obj.Name}' trong cùng cấp cha.",
                ApiCodeConstants.Common.DuplicatedData);

        // ── Compute new TreeIds ──
        var oldTreeIds = existData.TreeIds;
        string newTreeIds;

        if (obj.ParentCategoryId.HasValue)
        {
            var newParent = await _productCategoryRepository.GetByIdAsync(obj.ParentCategoryId.Value);
            newTreeIds = $"{newParent!.TreeIds},{existData.Id}";
        }
        else
        {
            newTreeIds = existData.Id.ToString();
        }

        obj.ToEntity(existData);
        existData.TreeIds = newTreeIds;

        await using var tx = await _productCategoryRepository.BeginTransactionAsync();
        try
        {
            await _productCategoryRepository.UpdateAsync(existData);
            await _productCategoryRepository.SaveChangesAsync();

            // Cascade update descendants' TreeIds
            if (oldTreeIds != newTreeIds)
            {
                var descendantPrefix = oldTreeIds + ",";
                var descendants = await _productCategoryRepository
                    .FindByCondition(x => !x.IsDeleted && x.TreeIds.StartsWith(descendantPrefix))
                    .ToListAsync();

                foreach (var desc in descendants)
                {
                    desc.TreeIds = newTreeIds + desc.TreeIds.Substring(oldTreeIds.Length);
                    await _productCategoryRepository.UpdateAsync(desc);
                }

                if (descendants.Any())
                    await _productCategoryRepository.SaveChangesAsync();
            }

            await _productCategoryRepository.EndTransactionAsync();
        }
        catch
        {
            await _productCategoryRepository.RollbackTransactionAsync();
            throw;
        }

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductCategoryDto> obj)
    {
        throw new NotImplementedException();
    }

    // ──────────────────────────────────────────────────────────
    // DELETE
    // ──────────────────────────────────────────────────────────
    /// Xóa mềm danh mục sản phẩm (IsDeleted = true). Block nếu còn child hoặc products.
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var existData = await _productCategoryRepository.GetByIdAsync(id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Block nếu còn child categories chưa xóa
        var hasChildren = await _productCategoryRepository.AnyAsync(x => !x.IsDeleted && x.ParentCategoryId == id);
        if (hasChildren)
            return ApiResponse.Conflict(
                "Không thể xóa danh mục đang có danh mục con chưa xóa.",
                ApiCodeConstants.Common.InvalidData);

        // Block nếu còn Products chưa xóa tham chiếu
        var hasProducts = await _productCategoryRepository
            .FindByCondition(x => x.Id == id)
            .Include(x => x.Products)
            .AnyAsync(x => x.Products.Any(p => !p.IsDeleted));

        if (hasProducts)
            return ApiResponse.Conflict(
                "Không thể xóa danh mục đang được sử dụng bởi sản phẩm chưa xóa.",
                ApiCodeConstants.Common.InvalidData);

        var isDeleted = await _productCategoryRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _productCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }
}
