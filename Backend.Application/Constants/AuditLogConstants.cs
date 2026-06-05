using System;

namespace Backend.Application.Constants;

public static class AuditLogConstants
{
    public static class TargetType
    {
        public static string NOTARIZATION_REQUEST = "NOTARIZATION_REQUEST";
    }

    public static readonly Dictionary<string, string> ListTargetType = new()
        {
            { "NOTARIZATION_REQUEST", "Hồ sơ công chứng" }
        };

    public static class AuditLogMessage
    {
        public const string NEW_NOTARIZATION_REQUEST = "Tạo mới yêu cầu công chứng với mã yêu cầu: {REQUEST_CODE}";
        public const string UPDATE_NOTARIZATION_REQUEST = "Cập nhật yêu cầu công chứng với mã yêu cầu: {REQUEST_CODE}";
    }
}
