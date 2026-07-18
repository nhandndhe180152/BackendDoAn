using System.Collections.Generic;
using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Domain.DTParameters;

namespace Backend.Domain.Interfaces.Repositories;

public interface ISalesOrderRepository : IRepositoryBase<SalesOrder, int>
{
    Task<SalesOrder?> GetByIdDetailAsync(int id);
    Task<List<SalesOrder>> GetPagedListAsync(string? keyword, int skip, int take);
    Task<int> CountAsync(string? keyword);
}
