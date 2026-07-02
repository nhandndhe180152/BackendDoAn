using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Products;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ (Business Logic) liên quan đến sản phẩm (Product)
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IStorageService _storageService;

    /// Khởi tạo ProductService với Repository được inject qua DI container
    public ProductService(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        IProductVariantRepository productVariantRepository,
        IStorageService storageService)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
    }

    // ──────────────────────────────────────────────────────────
    // CREATE
    // ──────────────────────────────────────────────────────────
    /// Tạo mới một sản phẩm
    public async Task<ApiResponse> CreateAsync(CreateProductDto obj)
    {
        var name = obj.Name?.Trim();
        if (string.IsNullOrEmpty(name))
            return ApiResponse.BadRequest(message: "Tên sản phẩm không được để trống.");

        // Validate ProductCategory
        var category = await _productCategoryRepository.GetByIdAsync(obj.ProductCategoryId);
        if (category == null || category.IsDeleted)
            return ApiResponse.UnprocessableEntity(
                "Danh mục sản phẩm không tồn tại hoặc đã bị xóa.",
                ApiCodeConstants.Common.InvalidData);

        var model = obj.ToEntity();
        model.IsDeleted = false;

        await _productRepository.CreateAsync(model);
        await _productRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    /// Tạo danh sách nhiều sản phẩm cùng lúc
    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateProductDto> objs)
    {
        var models = objs.Select(x => x.ToEntity()).ToList();
        await _productRepository.CreateListAsync(models);
        await _productRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    // ──────────────────────────────────────────────────────────
    // READ
    // ──────────────────────────────────────────────────────────
    /// Lấy danh sách toàn bộ sản phẩm đang hoạt động kèm tên danh mục
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ProductCategory)
            .Include(x => x.ProductVariants)
            .Select(x => x.ToDto())
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    /// Lấy chi tiết thông tin một sản phẩm theo ID
    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _productRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.ProductCategory)
            .Include(x => x.ProductVariants)
            .FirstOrDefaultAsync();

        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    /// Tìm kiếm và phân trang cơ bản theo từ khóa
    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _productRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ProductCategory)
            .Include(x => x.ProductVariants)
            .Select(x => x.ToDto());

        var totalRecord = await data.CountAsync();

        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.ProductCategoryName != null && x.ProductCategoryName.ToLower().Contains(query.Keyword.ToLower()));
        }

        if (!string.IsNullOrEmpty(query.OrderBy))
        {
            data = data.OrderByDynamic(query.OrderBy, query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);
        }

        var pagedData = new PagingData<ProductDetailDto>
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

    /// Phân trang nâng cao khớp bộ lọc DataTable ở Repository
    public async Task<ApiResponse> GetPagedAsync(ProductDTParameters parameters)
    {
        var data = await _productRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    /// Tìm kiếm và lọc sản phẩm chi tiết theo các tiêu chí cụ thể
    public async Task<ApiResponse> GetPagedAsync(ProductSearchQuery query)
    {
        IQueryable<Product> baseQuery = _productRepository.FindByCondition(x => true);

        if (!query.IncludeDeleted)
            baseQuery = baseQuery.Where(x => !x.IsDeleted);

        if (query.ProductCategoryId.HasValue)
        {
            if (query.IncludeDescendantCategories)
            {
                // Dùng subquery thay vì 2 round-trips:
                // Lấy target TreeIds bằng correlated subquery rồi lọc tất cả descendants trong 1 query
                var targetId = query.ProductCategoryId.Value;
                var targetTreeIds = await _productCategoryRepository
                    .FindByCondition(x => x.Id == targetId && !x.IsDeleted)
                    .Select(x => x.TreeIds)
                    .FirstOrDefaultAsync();

                if (targetTreeIds != null)
                {
                    var treePrefix = targetTreeIds + ",";
                    // Subquery: danh sách category khớp (chính nó + descendants)
                    var descendantCategoryIds = _productCategoryRepository
                        .FindByCondition(x => !x.IsDeleted &&
                            (x.TreeIds == targetTreeIds || x.TreeIds.StartsWith(treePrefix)))
                        .Select(x => x.Id);

                    baseQuery = baseQuery.Where(x => descendantCategoryIds.Contains(x.ProductCategoryId));
                }
                else
                {
                    // Category không tồn tại → trả rỗng
                    baseQuery = baseQuery.Where(x => x.ProductCategoryId == targetId);
                }
            }
            else
            {
                baseQuery = baseQuery.Where(x => x.ProductCategoryId == query.ProductCategoryId.Value);
            }
        }

        var data = baseQuery
            .Include(x => x.ProductCategory)
            .Include(x => x.ProductVariants)
            .Select(x => x.ToDto());

        // Apply filters trước khi đếm → totalRecord phản ánh đúng số sau filter
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var kw = query.Keyword.ToLower();
            data = data.Where(x =>
                x.Name.ToLower().Contains(kw) ||
                (x.Description != null && x.Description.ToLower().Contains(kw)) ||
                (x.ProductCategoryName != null && x.ProductCategoryName.ToLower().Contains(kw)));
        }

        if (query.IsActive.HasValue)
            data = data.Where(x => x.IsActive == query.IsActive.Value);

        var totalRecord = await data.CountAsync();

        if (!string.IsNullOrEmpty(query.OrderBy))
            data = data.OrderByDynamic(query.OrderBy,
                query.SortType == "asc" ? LinqExtensions.Order.Asc : LinqExtensions.Order.Desc);

        var pagedData = new PagingData<ProductDetailDto>
        {
            CurrentPage = query.PageIndex,
            PageSize = query.PageSize,
            DataSource = await data.Skip((query.PageIndex - 1) * query.PageSize).Take(query.PageSize).ToListAsync(),
            Total = totalRecord,
            TotalFiltered = totalRecord
        };

        return ApiResponse.Success(pagedData);
    }

    /// Lấy danh sách biến thể của sản phẩm theo ID
    public async Task<ApiResponse> GetVariantsByProductIdAsync(int id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        if (product == null || product.IsDeleted)
            return ApiResponse.NotFound();

        var variants = await _productVariantRepository
            .FindByCondition(x => x.ProductId == id && !x.IsDeleted)
            .Include(x => x.UnitOfMeasure)
            .Include(x => x.Image)
            .ToListAsync();

        var dtos = variants.Select(x => x.ToDto(
            x.Image != null ? _storageService.GetOriginalUrl(x.Image.FileKey) : null)).ToList();

        return ApiResponse.Success(dtos);
    }

    // ──────────────────────────────────────────────────────────
    // UPDATE
    // ──────────────────────────────────────────────────────────
    /// Cập nhật thông tin sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductDto obj)
    {
        var existData = await _productRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Validate ProductCategory
        var category = await _productCategoryRepository.GetByIdAsync(obj.ProductCategoryId);
        if (category == null || category.IsDeleted)
            return ApiResponse.UnprocessableEntity(
                "Danh mục sản phẩm không tồn tại hoặc đã bị xóa.",
                ApiCodeConstants.Common.InvalidData);

        obj.ToEntity(existData);
        await _productRepository.UpdateAsync(existData);
        await _productRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    /// Kích hoạt sản phẩm (IsActive = true)
    public async Task<ApiResponse> ActivateAsync(int id, int updatedBy)
    {
        var existData = await _productRepository.GetByIdAsync(id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        existData.IsActive = true;
        existData.UpdatedBy = updatedBy;
        existData.LastModifiedDate = DateTime.Now;

        await _productRepository.UpdateAsync(existData);
        await _productRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    /// Vô hiệu hóa sản phẩm (IsActive = false)
    public async Task<ApiResponse> DeactivateAsync(int id, int updatedBy)
    {
        var existData = await _productRepository.GetByIdAsync(id);
        if (existData == null || existData.IsDeleted)
            return ApiResponse.NotFound();

        existData.IsActive = false;
        existData.UpdatedBy = updatedBy;
        existData.LastModifiedDate = DateTime.Now;

        await _productRepository.UpdateAsync(existData);
        await _productRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductDto> obj)
    {
        throw new NotImplementedException();
    }

    // ──────────────────────────────────────────────────────────
    // DELETE
    // ──────────────────────────────────────────────────────────
    /// Xóa mềm một sản phẩm bằng cách chuyển trạng thái IsDeleted = true
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var existData = await _productRepository
            .FindByCondition(x => x.Id == id && !x.IsDeleted)
            .Include(x => x.ProductVariants)
            .FirstOrDefaultAsync();

        if (existData == null)
            return ApiResponse.NotFound();

        // Block nếu còn variants chưa xóa
        var hasVariants = existData.ProductVariants.Any(v => !v.IsDeleted);
        if (hasVariants)
            return ApiResponse.Conflict(
                "Không thể xóa sản phẩm đang có biến thể sản phẩm chưa xóa.",
                ApiCodeConstants.Common.InvalidData);

        var isDeleted = await _productRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _productRepository.SaveChangesAsync();

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }
}
