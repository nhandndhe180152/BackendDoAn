using System;
using System.Linq.Expressions;

namespace Backend.Share.Services;

public interface IScheduledJobService
{
    #region Fire And Forget

    string Enqueue(Expression<Action> functionCall);

    string Enqueue<T>(Expression<Action<T>> functionCall);
    #endregion

    #region Delayed Jobs

    string Schedule(Expression<Action> functionCall, TimeSpan delay);
    string Schedule<T>(Expression<Action<T>> functionCall, TimeSpan delay);
    string Schedule(Expression<Action> functionCall, DateTime enqueueAt);
    string Schedule<T>(Expression<Action<T>> functionCall, DateTime enqueueAt);
    #endregion

    #region Continuous Jobs
    string ContinueQueueWith(string parentJobId, Expression<Action> functionCall);
    #endregion

    bool Delete(string jobId);

    bool Requeue(string jobId);
    #region Recurring Jobs
    void AddOrUpdate(string name, Expression<Action> functionCall, string cron);
    void AddOrUpdate<T>(string name, Expression<Action<T>> functionCall, string cron);
    void DeleteRecurringJob(string name);
    #endregion
}
