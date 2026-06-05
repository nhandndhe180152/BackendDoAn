using System;
using Backend.Application.DTOs.ActivityLogs;
using Backend.Domain.Entities;

namespace Backend.Application.Mappings;

public static class ActivityLogMapping
{
    public static ActivityLog ToEntity(this CreateActivityLogDto obj)
    {
        return new ActivityLog
        {
            Action = obj.Action,
            Description = obj.Description,
            CreatedBy = obj.CreatedBy,
            CreatedDate = DateTime.Now
        };
    }
}
