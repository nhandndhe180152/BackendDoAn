namespace Backend.Application.Constants;

public static class OutboundOrderConstants
{
    public const int NoteMaxLength = 1000;
    public const int ManualUnlockReasonMaxLength = 500;
    public static readonly TimeSpan ColumnLockTimeout = TimeSpan.FromHours(24);
}
