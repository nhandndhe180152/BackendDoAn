using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Backend.Application.BackgroundJobs.FcmNotificationRetry;

public interface IFcmClient
{
    Task<string> SendAsync(string token, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken);
    Task<List<FcmSendOutcome>> SendMulticastAsync(List<string> tokens, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken);
}

public class FcmSendOutcome
{
    public string Token { get; set; } = null!;
    public bool IsSuccess { get; set; }
    public string? MessageId { get; set; }
    public Exception? Exception { get; set; }
}
