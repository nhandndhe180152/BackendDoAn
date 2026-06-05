using System;
using System.Linq.Expressions;
using Backend.Domain.Abstractions;
using Backend.Domain.Abstractions.Repositories;
using Backend.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.Infrastructure.Repositories;

public class RepositoryBase<TEntity, TKey> : IRepositoryBase<TEntity, TKey> where TEntity : EntityBase<TKey>
{
    private readonly BackendContext _context;
    private readonly IUnitOfWork _unitOfWork;

    public RepositoryBase(BackendContext context, IUnitOfWork unitOfWork)
    {
        _context = context;
        _unitOfWork = unitOfWork;
    }

    public bool Any(Expression<Func<TEntity, bool>> expression)
    {
        return _context.Set<TEntity>().Where(x => !x.IsDeleted).Any(expression);
    }

    public async Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _context.Set<TEntity>().Where(x => !x.IsDeleted).AnyAsync(expression);
    }

    public async Task<IDbContextTransaction> BeginTransactionAsync()
    {
        return await _context.Database.BeginTransactionAsync();
    }

    public async Task<int> CountByConditionAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _context.Set<TEntity>().Where(x => !x.IsDeleted).CountAsync(expression);
    }

    public async Task CreateAsync(TEntity entity)
    {
        await _context.Set<TEntity>().AddAsync(entity);
    }

    public async Task CreateListAsync(IEnumerable<TEntity> entities)
    {
        await _context.Set<TEntity>().AddRangeAsync(entities);
    }

    public async Task EndTransactionAsync()
    {
        await _context.Database.CommitTransactionAsync();
    }

    public IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false)
    {
        return trackChanges ? _context.Set<TEntity>().AsNoTracking().Where(x => !x.IsDeleted).Where(expression)
            : _context.Set<TEntity>().Where(x => !x.IsDeleted).Where(expression);
    }

    public IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        var items = FindByCondition(expression, trackChanges);
        items = includeProperties.Aggregate(items, (current, includeProperty) => current.Include(includeProperty));
        return items;
    }

    public async Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false)
    {
        return trackChanges ? await _context.Set<TEntity>().AsNoTracking().Where(x => !x.IsDeleted).Where(expression).ToListAsync()
           : await _context.Set<TEntity>().Where(x => !x.IsDeleted).Where(expression).ToListAsync();
    }

    public async Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        var items = FindByCondition(expression, trackChanges);
        items = includeProperties.Aggregate(items, (current, includeProperty) => current.Include(includeProperty));
        return await items.ToListAsync();
    }

    public async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool trackChanges = false, params Expression<Func<TEntity, object>>[]? includeProperties)
    {
        var query = GetAll(trackChanges);
        if (includeProperties != null)
            query = includeProperties.Aggregate(query, (current, include) => current.Include(include));

        return await query.FirstOrDefaultAsync(predicate);
    }

    public IQueryable<TEntity> GetAll(bool trackChanges = false)
    {
        return trackChanges ? _context.Set<TEntity>().Where(x => !x.IsDeleted).AsNoTracking()
            : _context.Set<TEntity>().Where(x => !x.IsDeleted);
    }

    public IQueryable<TEntity> GetAll(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        var items = GetAll(trackChanges);
        return items = includeProperties.Aggregate(items, (current, includeProperty) => current.Include(includeProperty));
    }

    public async Task<List<TEntity>> GetAllAsync(bool trackChanges = false)
    {
        return trackChanges ? await _context.Set<TEntity>().Where(x => !x.IsDeleted).AsNoTracking().ToListAsync()
            : await _context.Set<TEntity>().Where(x => !x.IsDeleted).ToListAsync();
    }

    public async Task<List<TEntity>> GetAllAsync(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        var items = GetAll(trackChanges);
        items = includeProperties.Aggregate(items, (current, includeProperty) => current.Include(includeProperty));
        return await items.ToListAsync();
    }

    public async Task<TEntity?> GetByIdAsync(TKey id)
    {
        return await FindByCondition(x => x.Id.Equals(id))
            .FirstOrDefaultAsync();
    }

    public async Task<TEntity?> GetByIdAsync(TKey id, params Expression<Func<TEntity, object>>[] includeProperties)
    {
        return await FindByCondition(x => x.Id.Equals(id), false, includeProperties)
            .FirstOrDefaultAsync();
    }

    public async Task HardDeleteAsync(TKey id)
    {
        var obj = await FindByCondition(x => x.Id.Equals(id))
            .FirstOrDefaultAsync();
        if (obj != null)
        {
            _context.Set<TEntity>().Remove(obj);
        }
    }

    public async Task HardDeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var objs = await FindByConditionAsync(predicate);
        if (objs.Any())
        {
            _context.Set<TEntity>().RemoveRange(objs);
        }
    }

    public async Task HardDeleteListAsync(IEnumerable<TKey> ids)
    {
        var objs = await FindByConditionAsync(x => ids.Contains(x.Id));
        if (objs.Any())
        {
            _context.Set<TEntity>().RemoveRange(objs);
        }
        await Task.CompletedTask;
    }

    public async Task<long> LongCountByConditionAsync(Expression<Func<TEntity, bool>> expression)
    {
        return await _context.Set<TEntity>().Where(x => !x.IsDeleted).LongCountAsync(expression);
    }

    public async Task RollbackTransactionAsync()
    {
        await _context.Database.RollbackTransactionAsync();
    }

    public async Task<int> SaveChangesAsync()
    {
        return await _unitOfWork.CommitAsync();
    }

    public async Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool trackChanges = false, params Expression<Func<TEntity, object>>[]? includeProperties)
    {
        var query = GetAll(trackChanges);
        if (includeProperties != null)
            query = includeProperties.Aggregate(query, (current, include) => current.Include(include));

        return await query.SingleOrDefaultAsync(predicate);
    }

    public async Task<bool> SoftDeleteAsync(TKey id)
    {
        var obj = await GetByIdAsync(id);
        if (obj != null)
        {
            obj.IsDeleted = true;

            _context.Attach(obj);
            _context.Entry(obj).Property(x => x.IsDeleted).IsModified = true;
            return true;
        }
        return false;
    }

    public async Task<bool> SoftDeleteAsync(Expression<Func<TEntity, bool>> predicate)
    {
        var objs = await FindByConditionAsync(predicate);
        if (objs.Any())
        {
            foreach (var obj in objs)
            {
                obj.IsDeleted = true;

                _context.Attach(obj);
                _context.Entry(obj).Property(x => x.IsDeleted).IsModified = true;
            }
        }

        return false;
    }

    public async Task<bool> SoftDeleteListAsync(IEnumerable<TKey> ids)
    {
        var objs = await FindByConditionAsync(x => ids.Contains(x.Id));
        if (objs.Any())
        {
            foreach (var obj in objs)
            {
                obj.IsDeleted = true;

                _context.Attach(obj);
                _context.Entry(obj).Property(x => x.IsDeleted).IsModified = true;
            }
        }
        return true;
    }

    public Task UpdateAsync(TEntity entity)
    {
        if (_context.Entry(entity).State == EntityState.Unchanged) return Task.CompletedTask;
        TEntity exist = _context.Set<TEntity>().Find(entity.Id);
        _context.Entry(exist).CurrentValues.SetValues(entity);
        return Task.CompletedTask;
    }

    public async Task UpdateListAsync(IEnumerable<TEntity> entities)
    {
        _context.Set<TEntity>().UpdateRange(entities);
        await Task.CompletedTask;
    }
}
