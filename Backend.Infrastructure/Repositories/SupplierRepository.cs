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

public class SupplierRepository : RepositoryBase<Supplier, int>, ISupplierRepository
{
    private readonly BackendContext _context;

    public SupplierRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<SupplierAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value?.Trim();
        var orderCriteria = "Id";
        var orderAscendingDirection = true;

        if (parameters.Order != null && parameters.Order.Any() && parameters.Columns != null && parameters.Columns.Any())
        {
            var rawColumn = parameters.Columns[parameters.Order[0].Column].Data;
            orderCriteria = NormalizeOrderColumn(rawColumn);
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }

        var query = _context.Suppliers
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new SupplierAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                ContactPerson = x.ContactPerson,
                Phone = x.Phone,
                Email = x.Email,
                Address = x.Address,
                TaxCode = x.TaxCode,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.Code.Contains(keyword) ||
                (x.ContactPerson != null && x.ContactPerson.Contains(keyword)) ||
                (x.Phone != null && x.Phone.Contains(keyword)) ||
                (x.Email != null && x.Email.Contains(keyword)) ||
                (x.Address != null && x.Address.Contains(keyword)) ||
                (x.TaxCode != null && x.TaxCode.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "isActive":
                    case "IsActive":
                        if (bool.TryParse(search, out var isActive))
                            query = query.Where(x => x.IsActive == isActive);
                        break;
                }
            }
        }

        var filteredRecord = await query.CountAsync();

        query = orderAscendingDirection
            ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc)
            : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

        var data = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();

        return new DTResult<SupplierAggregate>
        {
            draw = parameters.Draw,
            data = data,
            recordsFiltered = filteredRecord,
            recordsTotal = totalRecord
        };
    }

    private static string NormalizeOrderColumn(string? columnName)
    {
        return columnName switch
        {
            "id" => "Id",
            "name" => "Name",
            "code" => "Code",
            "contactPerson" => "ContactPerson",
            "phone" => "Phone",
            "email" => "Email",
            "address" => "Address",
            "taxCode" => "TaxCode",
            "isActive" => "IsActive",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}
