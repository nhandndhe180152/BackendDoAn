using System;
using Backend.Application.Interfaces;
using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace Backend.Infrastructure.Services;

public class FireBaseService : IFireBaseService
{
    private readonly ILogger<FireBaseService> _logger;
    public FireBaseService(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<FireBaseService>();
    }

    public async Task SendNotificationAsync(List<string> deviceTokens, string title, string body, string? categoryId, string? directionId)
    {
        if (deviceTokens == null || deviceTokens.Count == 0) return;

        try
        {
            const int maxTokensPerBatch = 500;
            for (int i = 0; i < deviceTokens.Count; i += maxTokensPerBatch)
            {
                var batchTokens = deviceTokens.Skip(i).Take(maxTokensPerBatch).ToList();

                var message = new MulticastMessage
                {
                    Tokens = batchTokens,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = new Dictionary<string, string>
                    {
                        { "categoryId", categoryId ?? "" },
                        { "directionId", directionId ?? "" }
                    }
                };

                try
                {
                    var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(message);

                    if (response.FailureCount > 0)
                    {
                        var failedTokens = new List<string>();

                        for (int j = 0; j < response.Responses.Count; j++)
                        {
                            if (!response.Responses[j].IsSuccess)
                            {
                                failedTokens.Add(batchTokens[j]);
                                _logger.LogError("[FCM] Failed to send to {Token}: {Message}", batchTokens[j], response.Responses[j].Exception?.Message);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FCM] Failed to send batch {Batch}: {Message}", (i / maxTokensPerBatch + 1), ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FCM] Failed to send notification: {Message}", ex.Message);
        }
    }
}