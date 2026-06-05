using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Products;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ (Business Logic) liên quan đến sản phẩm (Product)
public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    /// Khởi tạo ProductService với Repository được inject qua DI container
    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    /// Tạo mới một sản phẩm
    public async Task<ApiResponse> CreateAsync(CreateProductDto obj)
    {
        var model = obj.ToEntity();
        
        // Kiểm tra xem đã tồn tại sản phẩm trùng tên hay chưa (bỏ qua những sản phẩm đã bị xóa mềm)
        var isExistingProduct = await _productRepository.AnyAsync(
            x => x.Name.ToLower() == model.Name.ToLower() && !x.IsDeleted);

        if (isExistingProduct)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        // Lưu thông tin thực thể vào cơ sở dữ liệu
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

    /// Lấy danh sách toàn bộ sản phẩm đang hoạt động kèm tên danh mục
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ProductCategory)
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
            .Select(x => x.ToDto());

        var totalRecord = await data.CountAsync();
        
        // Lọc theo từ khóa tìm kiếm (Tên sản phẩm, Mô tả, hoặc Tên danh mục)
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.ProductCategoryName != null && x.ProductCategoryName.ToLower().Contains(query.Keyword.ToLower()));
        }

        // Sắp xếp động theo yêu cầu của client
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

    /// Phân trang nâng cao (chưa áp dụng mẫu tìm kiếm chung)
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

    /// Xóa mềm một sản phẩm bằng cách chuyển trạng thái IsDeleted = true
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _productRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _productRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    /// Xóa mềm danh sách nhiều sản phẩm cùng lúc
    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    /// Cập nhật thông tin sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductDto obj)
    {
        var existData = await _productRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Kiểm tra trùng lặp tên sản phẩm với các sản phẩm khác ngoại trừ chính sản phẩm đang sửa
        var isDuplicatedName = await _productRepository.AnyAsync(
            x => x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        // Chuyển đổi dữ liệu từ DTO cập nhật vào thực thể đang theo dõi
        obj.ToEntity(existData);
        await _productRepository.UpdateAsync(existData);
        await _productRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    /// Cập nhật danh sách nhiều sản phẩm
    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductDto> obj)
    {
        throw new NotImplementedException();
    }

    /// Tìm kiếm và lọc sản phẩm chi tiết theo các tiêu chí cụ thể
    public async Task<ApiResponse> GetPagedAsync(ProductSearchQuery query)
    {
        var data = _productRepository
            .FindByCondition(x => !x.IsDeleted)
            .Include(x => x.ProductCategory)
            .Select(x => x.ToDto());

        var totalRecord = await data.CountAsync();
        
        // Lọc theo từ khóa
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            data = data.Where(x => x.Name.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.Description != null && x.Description.ToLower().Contains(query.Keyword.ToLower()) ||
                                   x.ProductCategoryName != null && x.ProductCategoryName.ToLower().Contains(query.Keyword.ToLower()));
        }

        // Lọc theo Danh mục sản phẩm cụ thể
        if (query.ProductCategoryId.HasValue)
        {
            data = data.Where(x => x.ProductCategoryId == query.ProductCategoryId.Value);
        }

        // Lọc theo trạng thái hoạt động
        if (query.IsActive.HasValue)
        {
            data = data.Where(x => x.IsActive == query.IsActive.Value);
        }

        // Sắp xếp động
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
}
