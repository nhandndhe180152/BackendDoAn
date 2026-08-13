using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IProductVariantRepository : IRepositoryBase<ProductVariant, int>
{
    Task<DTResult<ProductVariantAggregate>> GetPagedAsync(ProductVariantDTParameters parameters);

    Task<ProductVariant?> GetActiveByIdAsync(int id);

    /// <summary>
    /// Checks the database-wide unique SKU, including soft-deleted variants.
    /// </summary>
    Task<bool> IsSkuInUseAsync(string sku, int? excludingId = null);
}
