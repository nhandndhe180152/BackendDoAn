using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface ITagRepository : IRepositoryBase<Tag, int>
{
    Task<DTResult<TagAggregate>> GetPagedAsync(TagDTParamters parameters);
}
