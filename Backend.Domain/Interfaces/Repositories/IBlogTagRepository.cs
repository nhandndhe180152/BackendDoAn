using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IBlogTagRepository : IRepositoryBase<BlogPostTag, int>
{
}
