using System;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Aggregates;
using Backend.Domain.DTParameters;
using Backend.Domain.Entities;
using Backend.Share.Entities;

namespace Backend.Domain.Interfaces.Repositories;

public interface INotificationRepository : IRepositoryBase<Notification, int>
{
    Task<DTResult<NotificationAggregate>> GetPagedAsync(NotificationDTParameters parameters);
}
