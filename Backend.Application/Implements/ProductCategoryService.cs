using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductCategories;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
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
    public ProductCategoryService(IProductCategoryRepository productCategoryRepository)
    {
        _productCategoryRepository = productCategoryRepository;
    }

    /// Tạo mới một danh mục sản phẩm
    public async Task<ApiResponse> CreateAsync(CreateProductCategoryDto obj)
    {
        var model = obj.ToEntity();
        
        // Kiểm tra trùng tên danh mục
        var isExistingCategory = await _productCategoryRepository.AnyAsync(
            x => x.Name.ToLower() == model.Name.ToLower() && !x.IsDeleted);

        if (isExistingCategory)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        await _productCategoryRepository.CreateAsync(model);
        await _productCategoryRepository.SaveChangesAsync();

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

    /// Xóa mềm danh mục sản phẩm (IsDeleted = true)
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
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

    /// Cập nhật thông tin danh mục sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductCategoryDto obj)
    {
        var existData = await _productCategoryRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Kiểm tra xem tên danh mục mới có bị trùng với danh mục khác không
        var isDuplicatedName = await _productCategoryRepository.AnyAsync(
            x => x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        obj.ToEntity(existData);
        await _productCategoryRepository.UpdateAsync(existData);
        await _productCategoryRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductCategoryDto> obj)
    {
        throw new NotImplementedException();
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
            data = data.Where(x => x.ParentId == query.ParentId.Value);
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
}
