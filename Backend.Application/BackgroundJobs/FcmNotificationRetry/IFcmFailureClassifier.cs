using System;

namespace Backend.Application.BackgroundJobs.FcmNotificationRetry;

public interface IFcmFailureClassifier
{
    string Classify(Exception exception);
}
