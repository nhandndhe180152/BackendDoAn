using System;
using System.Threading.Tasks;
using Backend.Application.DTOs.ProductVariants;
using Backend.Domain.DTParameters;
using Backend.Share.Entities;

namespace Backend.Application.Interfaces;

public interface IProductVariantService : IServiceBase<int, CreateProductVariantDto, UpdateProductVariantDto, ProductVariantDTParameters>
{
    Task<ApiResponse> GetPagedAsync(ProductVariantSearchQuery query);
    Task<ApiResponse> CheckSkuAsync(string sku, string? documentType = null, int? documentId = null);
    Task<ApiResponse> ConfirmScanAsync(ConfirmScanRequestDto request);
}
