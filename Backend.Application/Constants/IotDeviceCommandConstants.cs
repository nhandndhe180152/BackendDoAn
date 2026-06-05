using System;

namespace Backend.Application.Constants;

public static class IotDeviceCommandConstants
{
    public static class CommandType
    {
        public const string Tare = "TARE";
        public const string Reset = "RESET";
        public const string Calibrate = "CALIBRATE";
    }

    public static class Status
    {
        public const string Pending = "PENDING";
        public const string PickedUp = "PICKED_UP";
        public const string Executed = "EXECUTED";
        public const string Failed = "FAILED";
        public const string Expired = "EXPIRED";
        public const string Cancelled = "CANCELLED";
    }

    public static readonly string[] AllowedCommandTypes =
    {
        CommandType.Tare,
        CommandType.Reset,
        CommandType.Calibrate
    };

    public static readonly string[] AllowedStatuses =
    {
        Status.Pending,
        Status.PickedUp,
        Status.Executed,
        Status.Failed,
        Status.Expired,
        Status.Cancelled
    };
}
