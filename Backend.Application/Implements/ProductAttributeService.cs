using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductAttributes;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.DTParameters;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// Lớp triển khai nghiệp vụ liên quan đến Thuộc tính sản phẩm (Product Attribute)
public class ProductAttributeService : IProductAttributeService
{
    private readonly IProductAttributeRepository _productAttributeRepository;

    /// Khởi tạo ProductAttributeService
    public ProductAttributeService(IProductAttributeRepository productAttributeRepository)
    {
        _productAttributeRepository = productAttributeRepository;
    }

    /// Tạo mới một thuộc tính sản phẩm
    public async Task<ApiResponse> CreateAsync(CreateProductAttributeDto obj)
    {
        var model = obj.ToEntity();
        
        // Kiểm tra xem đã tồn tại thuộc tính trùng tên chưa
        var isExistingAttribute = await _productAttributeRepository.AnyAsync(
            x => x.Name.ToLower() == model.Name.ToLower() && !x.IsDeleted);

        if (isExistingAttribute)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        await _productAttributeRepository.CreateAsync(model);
        await _productAttributeRepository.SaveChangesAsync();

        return ApiResponse.Created(model.Id);
    }

    /// Tạo danh sách nhiều thuộc tính
    public async Task<ApiResponse> CreateListAsync(IEnumerable<CreateProductAttributeDto> objs)
    {
        var models = objs.Select(x => x.ToEntity()).ToList();
        await _productAttributeRepository.CreateListAsync(models);
        await _productAttributeRepository.SaveChangesAsync();
        return ApiResponse.Created(models.Select(x => x.Id));
    }

    /// Lấy danh sách toàn bộ thuộc tính sản phẩm
    public async Task<ApiResponse> GetAllAsync()
    {
        var data = await _productAttributeRepository
            .FindByCondition(x => !x.IsDeleted)
            .Select(x => x.ToDto())
            .ToListAsync();

        return ApiResponse.Success(data);
    }

    /// Lấy chi tiết thuộc tính theo ID
    public async Task<ApiResponse> GetByIdAsync(int id)
    {
        var data = await _productAttributeRepository.GetByIdAsync(id);
        if (data == null)
            return ApiResponse.NotFound();

        return ApiResponse.Success(data.ToDto());
    }

    /// Phân trang thuộc tính sản phẩm theo từ khóa
    public async Task<ApiResponse> GetPagedAsync(SearchQuery query)
    {
        var data = _productAttributeRepository
            .FindByCondition(x => !x.IsDeleted)
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

        var pagedData = new PagingData<ProductAttributeDetailDto>
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
    public async Task<ApiResponse> GetPagedAsync(ProductAttributeDTParameters parameters)
    {
        var data = await _productAttributeRepository.GetPagedAsync(parameters);
        return ApiResponse.Success(data);
    }

    /// Xóa mềm thuộc tính sản phẩm (IsDeleted = true)
    public async Task<ApiResponse> SoftDeleteAsync(int id)
    {
        var isDeleted = await _productAttributeRepository.SoftDeleteAsync(id);
        if (!isDeleted)
            return ApiResponse.BadRequest();

        await _productAttributeRepository.SaveChangesAsync();
        return ApiResponse.Success(isDeleted);
    }

    public Task<ApiResponse> SoftDeleteListAsync(IEnumerable<int> objs)
    {
        throw new NotImplementedException();
    }

    /// Cập nhật thông tin thuộc tính sản phẩm
    public async Task<ApiResponse> UpdateAsync(UpdateProductAttributeDto obj)
    {
        var existData = await _productAttributeRepository.GetByIdAsync(obj.Id);
        if (existData == null)
            return ApiResponse.NotFound();

        // Kiểm tra xem tên thuộc tính mới có trùng với thuộc tính khác không
        var isDuplicatedName = await _productAttributeRepository.AnyAsync(
            x => x.Name.ToLower() == obj.Name.ToLower() && x.Id != obj.Id && !x.IsDeleted);

        if (isDuplicatedName)
            return ApiResponse.UnprocessableEntity(
                ErrorMessagesConstants.GetMessage(ApiCodeConstants.Common.DuplicatedData).Replace("{key}", obj.Name),
                ApiCodeConstants.Common.DuplicatedData
            );

        obj.ToEntity(existData);
        await _productAttributeRepository.UpdateAsync(existData);
        await _productAttributeRepository.SaveChangesAsync();

        return ApiResponse.Success();
    }

    public Task<ApiResponse> UpdateListAsync(IEnumerable<UpdateProductAttributeDto> obj)
    {
        throw new NotImplementedException();
    }

    /// Tìm kiếm phân trang thuộc tính sản phẩm cơ bản
    public async Task<ApiResponse> GetPagedAsync(ProductAttributeSearchQuery query)
    {
        var data = _productAttributeRepository
            .FindByCondition(x => !x.IsDeleted)
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

        var pagedData = new PagingData<ProductAttributeDetailDto>
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
