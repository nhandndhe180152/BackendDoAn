using System;
using Backend.Domain.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class UnitOfWorkContext<TContext> : IUnitOfWorkContext<TContext> where TContext : DbContext
    {
        private readonly TContext _context;

        public UnitOfWorkContext(TContext context)
        {
            _context = context;
        }

        public async Task<int> CommitAsync()
        {
            return await _context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await _context.DisposeAsync();
        }
    }