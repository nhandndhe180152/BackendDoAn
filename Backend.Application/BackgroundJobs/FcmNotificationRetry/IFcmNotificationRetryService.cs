using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.FcmNotificationRetry;

public interface IFcmNotificationRetryService
{
    Task<FcmNotificationRetryResult> RunRetryJobAsync(CancellationToken cancellationToken);
}
