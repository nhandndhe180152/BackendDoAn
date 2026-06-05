using System;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IProductRepository : IRepositoryBase<Product, int>
{
    Task<DTResult<ProductAggregate>> GetPagedAsync(ProductDTParameters parameters);
}
