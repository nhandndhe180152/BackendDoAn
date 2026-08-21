using System;

namespace Backend.Domain.Enums;

public static class CustomerFeedbackStatus
{
    public const string Open = "OPEN";
    public const string Investigating = "INVESTIGATING";
    public const string Resolved = "RESOLVED";
    public const string Rejected = "REJECTED";

    public static readonly string[] All =
    {
        Open, Investigating, Resolved, Rejected
    };

    public static bool IsValid(string status)
    {
        if (string.IsNullOrWhiteSpace(status)) return false;
        return Array.Exists(All, s => s.Equals(status, StringComparison.OrdinalIgnoreCase));
    }
}
