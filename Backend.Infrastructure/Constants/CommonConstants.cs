using System;
using Backend.Domain.Entities;

namespace Backend.Infrastructure.Constants;

public static class CommonConstants
{
    public static readonly string[] AuditedEntityNames = new[]
    {
            nameof(User),
    };
}

