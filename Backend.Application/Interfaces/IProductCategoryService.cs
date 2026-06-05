using System;
using System.Threading.Tasks;
using Backend.Application.DTOs.ProductCategories;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IProductCategoryService : IServiceBase<int, CreateProductCategoryDto, UpdateProductCategoryDto, ProductCategoryDTParameters>
{
    Task<ApiResponse> GetPagedAsync(ProductCategorySearchQuery query);
}
