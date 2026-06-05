using System;
using System.Linq.Expressions;
using Hangfire;

namespace Backend.Share.Services;

public class ScheduledJobService : IScheduledJobService
{
    public void AddOrUpdate(string name, Expression<Action> functionCall, string cron)
    {
        RecurringJob.AddOrUpdate(name, functionCall, cron);
    }

    public void AddOrUpdate<T>(string name, Expression<Action<T>> functionCall, string cron)
    {
        RecurringJob.AddOrUpdate<T>(name, functionCall, cron);
    }

    public string ContinueQueueWith(string parentJobId, Expression<Action> functionCall)
    {
        return BackgroundJob.ContinueJobWith(parentJobId, functionCall);
    }

    public bool Delete(string jobId)
    {
        return BackgroundJob.Delete(jobId);
    }

    public void DeleteRecurringJob(string name)
    {
        RecurringJob.RemoveIfExists(name);
    }

    public string Enqueue(Expression<Action> functionCall)
    {
        return BackgroundJob.Enqueue(functionCall);
    }

    public string Enqueue<T>(Expression<Action<T>> functionCall)
    {
        return BackgroundJob.Enqueue<T>(functionCall);
    }

    public bool Requeue(string jobId)
    {
        return BackgroundJob.Requeue(jobId);
    }

    public string Schedule(Expression<Action> functionCall, TimeSpan delay)
    {
        return BackgroundJob.Schedule(functionCall, delay);
    }

    public string Schedule<T>(Expression<Action<T>> functionCall, TimeSpan delay)
    {
        return BackgroundJob.Schedule<T>(functionCall, delay);
    }

    public string Schedule(Expression<Action> functionCall, DateTime enqueueAt)
    {
        return BackgroundJob.Schedule(functionCall, enqueueAt);
    }

    public string Schedule<T>(Expression<Action<T>> functionCall, DateTime enqueueAt)
    {
        return BackgroundJob.Schedule<T>(functionCall, enqueueAt);
    }
}
