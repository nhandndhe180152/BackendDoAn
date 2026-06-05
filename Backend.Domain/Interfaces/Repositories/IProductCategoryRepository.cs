using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IProductCategoryRepository : IRepositoryBase<ProductCategory, int>
{
    Task<DTResult<ProductCategoryAggregate>> GetPagedAsync(ProductCategoryDTParameters parameters);
}
