using System;
using System.Globalization;
using Backend.Application.DTOs.SystemConfigs;
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

public class SystemConfigRepository : RepositoryBase<SystemConfig, int>, ISystemConfigRepository
    {
        private readonly BackendContext _context;
        public SystemConfigRepository(BackendContext context, IUnitOfWork unitOfWork) : base(context, unitOfWork)
        {
            _context = context;
        }

        public async Task<ApiResponse> GetPagedAsync(DTParameter parameters)
        {
            var keyword = parameters.Search?.Value;
            var orderCriteria = string.Empty;
            var orderAscendingDirection = true;
            if (parameters.Order != null)
            {
                orderCriteria = parameters.Columns[parameters.Order[0].Column].Data;
                orderAscendingDirection = parameters.Order[0].Dir.ToString().ToLower() == "desc";
            }
            else
            {
                orderCriteria = "Id";
                orderAscendingDirection = true;
            }

            var query = _context.SystemConfigs
                .Where(x => !x.IsDeleted)
                .Select(x => new SystemConfigListDto
                {
                    CreatedDate = x.CreatedDate,
                    Description = x.Description,
                    Id = x.Id,
                    Name = x.Name,
                    ConfigKey = x.ConfigKey,
                    ConfigValue = x.ConfigValue
                });

            var totalRecord = await query.CountAsync();

            if (!string.IsNullOrEmpty(keyword))
            {
                //keyword = keyword.ToLower();
                query = query
                    .Where(x => EF.Functions.Collate(x.Name, SQLParams.Latin_General).Contains(keyword) ||
                        (x.Description != null && EF.Functions.Collate(x.Description, SQLParams.Latin_General).Contains(keyword)) ||
                         EF.Functions.Collate(x.ConfigKey, SQLParams.Latin_General).Contains(keyword) ||
                         EF.Functions.Collate(x.ConfigValue, SQLParams.Latin_General).Contains(keyword) ||
                         x.CreatedDate.ToVietnameseDateTime().Contains(keyword)
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
                    case "description":
                        query = query.Where(r => r.Description != null && EF.Functions.Collate(r.Description, SQLParams.Latin_General).Contains(search));
                        break;
                    case "configKey":
                        query = query
                            .Where(r => EF.Functions.Collate(r.ConfigKey, SQLParams.Latin_General).Contains(search));
                        break;
                    case "configValue":
                        query = query
                            .Where(r => EF.Functions.Collate(r.ConfigValue, SQLParams.Latin_General).Contains(search));
                        break;
                    case "createdDate":
                        if (search.Contains(" - "))
                        {
                            var dates = search.Split(" - ");
                            var startDate = DateTime.ParseExact(dates[0], "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            var endDate = DateTime.ParseExact(dates[1], "dd/MM/yyyy", CultureInfo.InvariantCulture).AddDays(1).AddSeconds(-1);
                            query = query.Where(c => c.CreatedDate >= startDate && c.CreatedDate <= endDate);
                        }
                        else
                        {
                            var date = DateTime.ParseExact(search, "dd/MM/yyyy", CultureInfo.InvariantCulture);
                            query = query.Where(c => c.CreatedDate.Date == date.Date);
                        }
                        break;
                }
            }
            query = orderAscendingDirection ? query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Asc) : query.OrderByDynamic(orderCriteria, LinqExtensions.Order.Desc);

            var data = new DTResult<SystemConfigListDto>
            {
                draw = parameters.Draw,
                data = await query.Skip(parameters.Start).Take(parameters.Length).ToListAsync(),
                recordsFiltered = await query.CountAsync(),
                recordsTotal = totalRecord
            };

            return ApiResponse.Success(data);
        }

        public async Task<string> GetValueByKey(string key)
        {
            var value = await _context.SystemConfigs
                .Where(x => x.ConfigKey == key && !x.IsDeleted)
                .Select(x => x.ConfigValue)
                .FirstOrDefaultAsync();
            return value ?? string.Empty;
        }
    }
