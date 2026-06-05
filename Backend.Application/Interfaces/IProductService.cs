using System;
using System.Threading.Tasks;
using Backend.Application.DTOs.Products;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IProductService : IServiceBase<int, CreateProductDto, UpdateProductDto, ProductDTParameters>
{
    Task<ApiResponse> GetPagedAsync(ProductSearchQuery query);
}
