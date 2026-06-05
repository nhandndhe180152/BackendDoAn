using System;
using Backend.Domain.Abstractions;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;

namespace Backend.Infrastructure.Repositories;

public class BlogTagRepository : RepositoryBase<BlogPostTag, int>, IBlogTagRepository
{
    public BlogTagRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
    }
}
