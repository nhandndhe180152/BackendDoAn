using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.Domain.Abstractions.Repositories;

/// <summary>
    /// Interface of repository base for multi context
    /// </summary>
    /// <typeparam name="TEntity">Entity</typeparam>
    /// <typeparam name="Tkey">Type of id column</typeparam>
    /// <typeparam name="TContext">DbContext</typeparam>
    public interface IRepositoryBaseDbContext<TEntity, in Tkey, TContext> where TEntity : EntityBase<Tkey> where TContext : DbContext
    {
        IQueryable<TEntity> GetAll(bool trackChanges = false);
        IQueryable<TEntity> GetAll(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        Task<List<TEntity>> GetAllAsync(bool trackChanges = false);
        Task<List<TEntity>> GetAllAsync(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false);
        IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false);
        Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        bool Any(Expression<Func<TEntity, bool>> expression);
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression);
        Task<TEntity?> GetByIdAsync(Tkey id);
        Task<TEntity?> GetByIdAsync(Tkey id, params Expression<Func<TEntity, object>>[] includeProperties);
        Task<int> CountByConditionAsync(Expression<Func<TEntity, bool>> expression);
        Task<long> LongCountByConditionAsync(Expression<Func<TEntity, bool>> expression);
        Task CreateAsync(TEntity entity);
        Task CreateListAsync(IEnumerable<TEntity> entities);
        Task UpdateAsync(TEntity entity);
        Task UpdateListAsync(IEnumerable<TEntity> entities);
        Task<bool> SoftDeleteAsync(Tkey id);
        Task HardDeleteAsync(Tkey id);
        Task<bool> SoftDeleteListAsync(IEnumerable<Tkey> ids);
        Task HardDeleteListAsync(IEnumerable<Tkey> ids);
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task EndTransactionAsync();
        Task RollbackTransactionAsync();
    }
