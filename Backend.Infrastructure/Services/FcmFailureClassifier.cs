using System;
using Backend.Application.BackgroundJobs.FcmNotificationRetry;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;

namespace Backend.Infrastructure.Services;

public class FcmFailureClassifier : IFcmFailureClassifier
{
    public string Classify(Exception exception)
    {
        if (exception == null)
            return FcmNotificationRetryConstants.Outcome.Success;

        // 1. Check if it's a direct FirebaseMessagingException
        if (exception is FirebaseMessagingException fcmEx)
        {
            return MapMessagingErrorCode(fcmEx.MessagingErrorCode, fcmEx.Message);
        }

        // 2. Check inner exception
        if (exception.InnerException is FirebaseMessagingException innerFcmEx)
        {
            return MapMessagingErrorCode(innerFcmEx.MessagingErrorCode, innerFcmEx.Message);
        }

        // 3. Check for general FirebaseException (often credentials, transport, etc.)
        if (exception is FirebaseException fbEx)
        {
            return MapFirebaseException(fbEx);
        }

        // 4. Fallback: string-based heuristic checks (highly robust for network timeouts/auth failures)
        var msg = exception.ToString().ToLowerInvariant();

        if (msg.Contains("unregistered") || 
            msg.Contains("not registered") || 
            msg.Contains("invalid-registration-token") || 
            msg.Contains("invalid token") || 
            msg.Contains("bad_device_token") ||
            msg.Contains("token is invalid"))
        {
            return FcmNotificationRetryConstants.Outcome.InvalidToken;
        }

        if (msg.Contains("permission denied") || 
            msg.Contains("permission_denied") || 
            msg.Contains("unauthorized") || 
            msg.Contains("credentials") || 
            msg.Contains("authentication") || 
            msg.Contains("project id") || 
            msg.Contains("service account") ||
            msg.Contains("auth") ||
            msg.Contains("401") ||
            msg.Contains("403"))
        {
            return FcmNotificationRetryConstants.Outcome.GlobalConfiguration;
        }

        if (msg.Contains("timeout") || 
            msg.Contains("unavailable") || 
            msg.Contains("temporarily unavailable") || 
            msg.Contains("resource exhausted") || 
            msg.Contains("too many requests") || 
            msg.Contains("limit") || 
            msg.Contains("429") || 
            msg.Contains("500") || 
            msg.Contains("503") ||
            msg.Contains("connection reset") ||
            msg.Contains("dns") ||
            msg.Contains("network"))
        {
            return FcmNotificationRetryConstants.Outcome.TransientMessage;
        }

        if (msg.Contains("payload") || 
            msg.Contains("invalid-argument") || 
            msg.Contains("title") || 
            msg.Contains("body") || 
            msg.Contains("malformed"))
        {
            return FcmNotificationRetryConstants.Outcome.PayloadInvalid;
        }

        return FcmNotificationRetryConstants.Outcome.Unknown;
    }

    private string MapMessagingErrorCode(MessagingErrorCode? errorCode, string message)
    {
        if (errorCode == null)
            return FcmNotificationRetryConstants.Outcome.Unknown;

        switch (errorCode)
        {
            case MessagingErrorCode.Unregistered:
            case MessagingErrorCode.SenderIdMismatch:
                return FcmNotificationRetryConstants.Outcome.InvalidToken;

            case MessagingErrorCode.InvalidArgument:
                if (message.ToLowerInvariant().Contains("token"))
                {
                    return FcmNotificationRetryConstants.Outcome.InvalidToken;
                }
                return FcmNotificationRetryConstants.Outcome.PayloadInvalid;

            case MessagingErrorCode.QuotaExceeded:
            case MessagingErrorCode.Unavailable:
            case MessagingErrorCode.Internal:
                return FcmNotificationRetryConstants.Outcome.TransientMessage;

            case MessagingErrorCode.ThirdPartyAuthError:
                return FcmNotificationRetryConstants.Outcome.GlobalConfiguration;

            default:
                return FcmNotificationRetryConstants.Outcome.Unknown;
        }
    }

    private string MapFirebaseException(FirebaseException exception)
    {
        var msg = exception.Message.ToLowerInvariant();
        if (msg.Contains("credential") || msg.Contains("permission") || msg.Contains("auth"))
        {
            return FcmNotificationRetryConstants.Outcome.GlobalConfiguration;
        }
        return FcmNotificationRetryConstants.Outcome.Unknown;
    }
}
