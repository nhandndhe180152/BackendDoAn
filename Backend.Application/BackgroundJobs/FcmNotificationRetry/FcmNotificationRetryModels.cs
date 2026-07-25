using System;

namespace Backend.Application.BackgroundJobs.FcmNotificationRetry;

public static class FcmNotificationRetryConstants
{
    public static class Status
    {
        public const string Pending = "PENDING";
        public const string Processing = "PROCESSING";
        public const string RetryScheduled = "RETRY_SCHEDULED";
        public const string Sent = "SENT";
        public const string FailedPermanent = "FAILED_PERMANENT";
        public const string Exhausted = "EXHAUSTED";
        public const string Stale = "STALE";
    }

    public static class Outcome
    {
        public const string Success = "SUCCESS";
        public const string TransientMessage = "TRANSIENT_MESSAGE";
        public const string PermanentMessage = "PERMANENT_MESSAGE";
        public const string InvalidToken = "INVALID_TOKEN";
        public const string GlobalTransient = "GLOBAL_TRANSIENT";
        public const string GlobalConfiguration = "GLOBAL_CONFIGURATION";
        public const string PayloadInvalid = "PAYLOAD_INVALID";
        public const string Stale = "STALE";
        public const string Unknown = "UNKNOWN";
    }

    public static class Job
    {
        public const string Id = "job-05-fcm-notification-retry";
        public const string LogPrefix = "[JOB-05]";
        public const string LockKeyPrefix = "stocklite:job-05:fcm-notification-retry";
        public const string DeduplicationKeyPrefix = "FCM_NOTIFICATION_RETRY";
        public const string ConfigAlertType = "FCM_RETRY_CONFIGURATION";
    }

    public static class DefaultOptions
    {
        public const int MaxAttempts = 3;
        public const int BatchSize = 500;
        public const int ProcessingLeaseDurationMinutes = 5;
        public const double InitialBackoffSeconds = 1.0;
        public const double MaxBackoffSeconds = 30.0;
    }
}

public class FcmNotificationRetryResult
{
    public int Eligible { get; set; }
    public int Claimed { get; set; }
    public int Processed { get; set; }
    public int Sent { get; set; }
    public int RetryScheduled { get; set; }
    public int PermanentFailed { get; set; }
    public int InvalidTokens { get; set; }
    public int DevicesRevoked { get; set; }
    public int Exhausted { get; set; }
    public int Stale { get; set; }
    public int PayloadInvalid { get; set; }
    public int LeaseRecovered { get; set; }
    public int Skipped { get; set; }
    public int Failed { get; set; }
    public long DurationMs { get; set; }
    public bool SkippedByLock { get; set; }
}
