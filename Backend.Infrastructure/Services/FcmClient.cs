using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
using FirebaseAdmin.Messaging;

namespace Backend.Infrastructure.Services;

public class FcmClient : IFcmClient
{
    public Task<string> SendAsync(string token, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Token = token,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data ?? new Dictionary<string, string>()
        };

        return FirebaseMessaging.DefaultInstance.SendAsync(message, cancellationToken);
    }

    public async Task<List<FcmSendOutcome>> SendMulticastAsync(List<string> tokens, string title, string body, Dictionary<string, string>? data, CancellationToken cancellationToken)
    {
        if (tokens == null || tokens.Count == 0)
            return new List<FcmSendOutcome>();

        var message = new MulticastMessage
        {
            Tokens = tokens,
            Notification = new Notification
            {
                Title = title,
                Body = body
            },
            Data = data ?? new Dictionary<string, string>()
        };

        var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message, cancellationToken);

        var outcomes = new List<FcmSendOutcome>();
        for (int i = 0; i < tokens.Count; i++)
        {
            var r = response.Responses[i];
            outcomes.Add(new FcmSendOutcome
            {
                Token = tokens[i],
                IsSuccess = r.IsSuccess,
                MessageId = r.IsSuccess ? r.MessageId : null,
                Exception = r.IsSuccess ? null : r.Exception
            });
        }

        return outcomes;
    }
}
