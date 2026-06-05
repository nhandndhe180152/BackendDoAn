using System;

namespace Backend.Domain.Abstractions;

public interface IUnitOfWork : IAsyncDisposable
{
    Task<int> CommitAsync();
}