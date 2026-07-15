using System.Threading.Tasks;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface IPaddyPurchaseReceiptRepository : IRepositoryBase<PaddyPurchaseReceipt, int>
{
    Task<DTResult<PaddyPurchaseReceiptAggregate>> GetPagedAsync(DTParameter parameters);
}
