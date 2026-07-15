using System;
using System.Linq;
using System.Threading.Tasks;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class DebtTransactionRepository : RepositoryBase<DebtTransaction, int>, IDebtTransactionRepository
{
    private readonly BackendContext _context;

    public DebtTransactionRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<DebtTransactionAggregate>> GetPagedByPartyDebtAsync(int partyDebtId, DTParameter parameters)
    {
        var orderCriteria = "TransactionDate";
        var orderAscendingDirection = false;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.DebtTransactions
            .AsNoTracking()
            .Where(x => x.PartyDebtId == partyDebtId && !x.IsDeleted)
            .Select(x => new DebtTransactionAggregate
            {
                Id = x.Id,
                PartyDebtId = x.PartyDebtId,
                TransactionType = x.TransactionType,
                Amount = x.Amount,
                BalanceAfter = x.BalanceAfter,
                RefType = x.RefType,
                RefId = x.RefId,
                TransactionDate = x.TransactionDate,
                DueDate = x.DueDate,
                Note = x.Note,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return new DTResult<DebtTransactionAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = totalRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName) => columnName switch
    {
        "transactionDate" => "TransactionDate",
        "amount" => "Amount",
        "transactionType" => "TransactionType",
        _ => "TransactionDate"
    };
}
