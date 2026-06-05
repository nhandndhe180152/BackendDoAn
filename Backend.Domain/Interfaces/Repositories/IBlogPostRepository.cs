using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IBlogPostRepository : IRepositoryBase<BlogPost, int>
{
    Task<DTResult<BlogPostAggregate>> GetPagedAsync(BlogPostDTParameters parameters);
    Task<PagingData<BlogPostAggregate>> GetPagedClientAsync(BlogPostSearchDTParameters parameters);
}
