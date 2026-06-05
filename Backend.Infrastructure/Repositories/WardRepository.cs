using System;
using System.Globalization;
using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Domain.Aggregates;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Infrastructure.Persistence;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Microsoft.EntityFrameworkCore;

namespace Backend.Infrastructure.Repositories;

public class WardRepository : RepositoryBase<Ward, int>, IWardRepository
{
    private readonly BackendContext _context;
    public WardRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
    {
        _context = context;
    }

    public async Task<DTResult<WardAggregate>> GetPagedAsync(DTParameter parameters)
    {
        var keyword = parameters.Search?.Value;
        var orderCriteria = string.Empty;
        var orderAscendingDirection = true;
        if (parameters.Order != null && parameters.Order.Any())
        {
            orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
            orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "asc";
        }
        else
        {
            orderCriteria = "Id";
            orderAscendingDirection = true;
        }

        var query = _context.Wards
            .Where(x => !x.IsDeleted)
            .Select(x => new WardAggregate
            {
                Id = x.Id,
                Name = x.Name,
                Code = x.Code,
                Slug = x.Slug,
                Type = x.Type,
                ProvinceCode = x.ProvinceCode,
                ProvinceId = x.ProvinceId,
                ProvinceName = x.Province.Name
            });

        var totalRecord = await query.CountAsync();

        if (!string.IsNullOrEmpty(keyword))
        {
            //keyword = keyword.ToLower();
            query = query
                .Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.Code, SQLParams.Latin_General).Contains(keyword) ||
                    EF.Functions.Collate(x.ProvinceName, SQLParams.Latin_General).Contains(keyword) ||
                    (x.Slug != null && EF.Functions.Collate(x.Slug, SQLParams.Latin_General).Contains(keyword)) ||
                    (x.Type != null && EF.Functions.Collate(x.Type, SQLParams.Latin_General).Contains(keyword))
                );
        }
        foreach (var column in parameters.Columns)
        {
            var search = column.Search.Value;
            if (!search.Contains("/"))
            {
                search = column.Search.Value.ToLower();
            }
            if (string.IsNullOrEmpty(search)) continue;
            switch (column.Data)
            {
                case "name":
                    query = query
                        .Where(r => EF.Functions.Collate(r.Name, SQLParams.Latin_General).Contains(search));
                    break;
                case "provinceId":
                    var provinceArr = search.Split(",");
                    query = query
                        .Where(r => provinceArr.Contains(r.ProvinceId.ToString()));
                    break;
                case "type":
                    var typeArr = search.Split(",");
                    query = query.Where(r => r.Type != null && typeArr.Contains(r.Type));
                    break;
            }
        }
        query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);
        var records = await query
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync();
        records.ForEach(item => item.TypeName = CommonConstants.WardTypes.GetValueOrDefault(item.Type) ?? string.Empty);
        var data = new DTResult<WardAggregate>
        {
            draw = parameters.Draw,
            data = records,
            recordsFiltered = await query.CountAsync(),
            recordsTotal = totalRecord
        };

        return data;
    }
}
