using System;
using Microsoft.EntityFrameworkCore;

namespace Backend.Domain.Abstractions;

public interface IUnitOfWorkContext<TContext> : IAsyncDisposable where TContext : DbContext
{
    Task<int> CommitAsync();
}
