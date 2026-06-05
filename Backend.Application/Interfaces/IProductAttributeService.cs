using System;
using System.Threading.Tasks;
using Backend.Application.DTOs.ProductAttributes;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IProductAttributeService : IServiceBase<int, CreateProductAttributeDto, UpdateProductAttributeDto, ProductAttributeDTParameters>
{
    Task<ApiResponse> GetPagedAsync(ProductAttributeSearchQuery query);
}
