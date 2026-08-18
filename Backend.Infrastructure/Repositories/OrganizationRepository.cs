using System;
using System.Globalization;
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

public class OrganizationRepository : RepositoryBase<Organization, int>, IOrganizationRepository
{
    private readonly BackendContext _context;

    public OrganizationRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<OrganizationAggregate>> GetPagedAsync(DTParameter parameters)
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

        var query = _context.Organizations
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .Select(x => new OrganizationAggregate
            {
                Id = x.Id,
                Code = x.Code,
                Name = x.Name,
                Description = x.Description,
                TaxCode = x.TaxCode,
                Address = x.Address,
                ContactEmail = x.ContactEmail,
                ContactPhone = x.ContactPhone,
                LogoFileId = x.LogoFileId,
                SubscriptionPlan = x.SubscriptionPlan,
                SubscriptionExpiry = x.SubscriptionExpiry,
                IsActive = x.IsActive,
                CreatedDate = x.CreatedDate
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            query = query.Where(x =>
                x.Name.Contains(keyword) ||
                x.Code.Contains(keyword) ||
                (x.TaxCode != null && x.TaxCode.Contains(keyword)) ||
                (x.ContactEmail != null && x.ContactEmail.Contains(keyword)) ||
                (x.ContactPhone != null && x.ContactPhone.Contains(keyword)) ||
                (x.Address != null && x.Address.Contains(keyword)));
        }

        if (parameters.Columns != null)
        {
            foreach (var column in parameters.Columns)
            {
                var search = column.Search?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(search)) continue;

                switch (column.Data)
                {
                    case "name":
                    case "Name":
                        query = query.Where(x => x.Name.Contains(search));
                        break;
                    case "code":
                    case "Code":
                        query = query.Where(x => x.Code.Contains(search));
                        break;
                    case "taxCode":
                    case "TaxCode":
                        query = query.Where(x => x.TaxCode != null && x.TaxCode.Contains(search));
                        break;
                    case "contactEmail":
                    case "ContactEmail":
                        query = query.Where(x => x.ContactEmail != null && x.ContactEmail.Contains(search));
                        break;
                    case "contactPhone":
                    case "ContactPhone":
                        query = query.Where(x => x.ContactPhone != null && x.ContactPhone.Contains(search));
                        break;
                    case "isActive":
                    case "IsActive":
                        if (bool.TryParse(search, out var isActive))
                            query = query.Where(x => x.IsActive == isActive);
                        break;
                    case "createdDate":
                    case "CreatedDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(x => x.CreatedDate >= startDate && x.CreatedDate <= endDate);
                        }
                        else if (DateTime.TryParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                        {
                            query = query.Where(x => x.CreatedDate.Date == date.Date);
                        }
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

        return new DTResult<OrganizationAggregate>
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
            "taxCode" => "TaxCode",
            "contactEmail" => "ContactEmail",
            "contactPhone" => "ContactPhone",
            "isActive" => "IsActive",
            "createdDate" => "CreatedDate",
            _ => "Id"
        };
    }
}
