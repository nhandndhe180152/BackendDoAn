using System;

namespace Backend.Infrastructure.Services;

public interface IScheduledJob
{
    Task ExecuteAsync();
}
