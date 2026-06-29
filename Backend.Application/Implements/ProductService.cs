using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Application.DTOs.Products;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ (Business Logic) liên quan đến sản phẩm (Product)
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IProductCategoryRepository _productCategoryRepository;
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IStorageService _storageService;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// Khởi tạo ProductService với Repository được inject qua DI container
    public ProductService(
        IProductRepository productRepository,
        IProductCategoryRepository productCategoryRepository,
        IProductVariantRepository productVariantRepository,
        IStorageService storageService,
        IAuditLogRepository auditLogRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _productRepository = productRepository;
        _productCategoryRepository = productCategoryRepository;
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
        _auditLogRepository = auditLogRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    // ──────────────────────────────────────────────────────────
    // Helper: Audit
    // ──────────────────────────────────────────────────────────
    private async Task LogAuditAsync(string action, string targetId, string description,
        string? dataBefore = null, string? dataAfter = null)
    {
        var ctx = _httpContextAccessor.HttpContext;
        var audit = new AuditLog
        {
            Action = action,
            TargetType = "Product",
            TargetId = targetId,
            DataBefore = dataBefore,
            DataAfter = dataAfter,
            Description = description,
            IpAddress = ctx?.GetRemoteHostIpAddress(),
            UserAgent = ctx?.Request?.Headers["User-Agent"].ToString(),
            CreatedBy = ctx?.GetCurrentUserId(),
            CreatedDate = DateTime.Now
        };
        await _auditLogRepository.CreateAsync(audit);
        await _auditLogRepository.SaveChangesAsync();
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

        await LogAuditAsync("CREATE", model.Id.ToString(), $"Tạo sản phẩm '{model.Name}'");

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
        // Base query
        IQueryable<Product> baseQuery;

        if (query.IncludeDescendantCategories && query.ProductCategoryId.HasValue)
        {
            // Load the selected category to get its TreeIds prefix
            var cat = await _productCategoryRepository.GetByIdAsync(query.ProductCategoryId.Value);
            if (cat != null)
            {
                var treePrefix = cat.TreeIds + ",";
                // Include all categories whose TreeIds equals the target or starts with target TreeIds + ","
                var matchingCategoryIds = await _productCategoryRepository
                    .FindByCondition(x => !x.IsDeleted &&
                        (x.TreeIds == cat.TreeIds || x.TreeIds.StartsWith(treePrefix)))
                    .Select(x => x.Id)
                    .ToListAsync();

                baseQuery = _productRepository
                    .FindByCondition(x => matchingCategoryIds.Contains(x.ProductCategoryId));
            }
            else
            {
                baseQuery = _productRepository.FindByCondition(x => x.ProductCategoryId == query.ProductCategoryId.Value);
            }
        }
        else if (query.ProductCategoryId.HasValue)
        {
            baseQuery = _productRepository.FindByCondition(x => x.ProductCategoryId == query.ProductCategoryId.Value);
        }
        else
        {
            baseQuery = _productRepository.FindByCondition(x => true);
        }

        if (!query.IncludeDeleted)
            baseQuery = baseQuery.Where(x => !x.IsDeleted);

        var data = baseQuery
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

        if (query.IsActive.HasValue)
        {
            data = data.Where(x => x.IsActive == query.IsActive.Value);
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

        var dataBefore = $"Name={existData.Name},CategoryId={existData.ProductCategoryId},IsActive={existData.IsActive}";

        obj.ToEntity(existData);
        await _productRepository.UpdateAsync(existData);
        await _productRepository.SaveChangesAsync();

        await LogAuditAsync("UPDATE", existData.Id.ToString(),
            $"Cập nhật sản phẩm '{existData.Name}'", dataBefore,
            $"Name={existData.Name},CategoryId={existData.ProductCategoryId},IsActive={existData.IsActive}");

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

        await LogAuditAsync("UPDATE", id.ToString(), $"Kích hoạt sản phẩm '{existData.Name}'",
            "IsActive=false", "IsActive=true");

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

        await LogAuditAsync("UPDATE", id.ToString(), $"Vô hiệu hóa sản phẩm '{existData.Name}'",
            "IsActive=true", "IsActive=false");

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

        await LogAuditAsync("DELETE", id.ToString(), $"Xóa mềm sản phẩm '{existData.Name}'");

        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }
}
