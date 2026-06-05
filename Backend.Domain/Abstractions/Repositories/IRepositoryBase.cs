using System;
using System.Linq.Expressions;
using Backend.Domain.Abstractions.Entities;
using Microsoft.EntityFrameworkCore.Storage;

namespace Backend.Domain.Abstractions.Repositories;

public interface IReadRepository<TEntity, in TKey> where TEntity : IEntityBase<TKey>
    {
        IQueryable<TEntity> GetAll(bool trackChanges = false);
        IQueryable<TEntity> GetAll(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        Task<List<TEntity>> GetAllAsync(bool trackChanges = false);
        Task<List<TEntity>> GetAllAsync(bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);

        IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false);
        IQueryable<TEntity> FindByCondition(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);
        Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false);
        Task<List<TEntity>> FindByConditionAsync(Expression<Func<TEntity, bool>> expression, bool trackChanges = false, params Expression<Func<TEntity, object>>[] includeProperties);

        Task<TEntity?> GetByIdAsync(TKey id);
        Task<TEntity?> GetByIdAsync(TKey id, params Expression<Func<TEntity, object>>[] includeProperties);

        bool Any(Expression<Func<TEntity, bool>> expression);
        Task<bool> AnyAsync(Expression<Func<TEntity, bool>> expression);
        Task<int> CountByConditionAsync(Expression<Func<TEntity, bool>> expression);
        Task<long> LongCountByConditionAsync(Expression<Func<TEntity, bool>> expression);

        Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool trackChanges = false, params Expression<Func<TEntity, object>>[]? includeProperties);
        Task<TEntity?> SingleOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, bool trackChanges = false, params Expression<Func<TEntity, object>>[]? includeProperties);
    }

    public interface IWriteRepository<TEntity>
    {
        Task CreateAsync(TEntity entity);
        Task CreateListAsync(IEnumerable<TEntity> entities);
        Task UpdateAsync(TEntity entity);
        Task UpdateListAsync(IEnumerable<TEntity> entities);
    }
    public interface IDeleteRepository<TEntity, in TKey>
    {
        Task<bool> SoftDeleteAsync(TKey id);
        Task<bool> SoftDeleteAsync(Expression<Func<TEntity, bool>> predicate);
        Task<bool> SoftDeleteListAsync(IEnumerable<TKey> ids);

        Task HardDeleteAsync(TKey id);
        Task HardDeleteAsync(Expression<Func<TEntity, bool>> predicate);
        Task HardDeleteListAsync(IEnumerable<TKey> ids);

    }

    /// <summary>
    /// Interface of repository base for single context
    /// </summary>
    /// <typeparam name="TEntity">Entity</typeparam>
    /// <typeparam name="Tkey">Data type of id column</typeparam>
    public interface IRepositoryBase<TEntity, in Tkey> : 
        IReadRepository<TEntity, Tkey>,
        IWriteRepository<TEntity>,
        IDeleteRepository<TEntity, Tkey> 
        where TEntity : EntityBase<Tkey>
    {
        Task<int> SaveChangesAsync();
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task EndTransactionAsync();
        Task RollbackTransactionAsync();
    }
