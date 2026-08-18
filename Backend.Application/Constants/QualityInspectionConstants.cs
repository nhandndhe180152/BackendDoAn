namespace Backend.Application.Constants;

/// <summary>
/// Kết quả kiểm tra cấp bao.
/// PASS: bao đạt yêu cầu.
/// ISSUE_DETECTED: bao có vấn đề (không dùng FAIL — quyết định xử lý do Disposition).
/// </summary>
public static class BagQualityResultConstants
{
    public const string Pass          = "PASS";
    public const string IssueDetected = "ISSUE_DETECTED";
}

/// <summary>
/// Quyết định xử lý bao sau kiểm tra.
/// Context phụ thuộc InspectionType — xem comment từng nhóm.
/// </summary>
public static class BagDispositionConstants
{
    // ── Receiving (nhận hàng đầu vào) ─────────────────────────────────────────
    /// <summary>Chấp nhận, nhập kho bình thường.</summary>
    public const string AcceptNormal     = "ACCEPT_NORMAL";
    /// <summary>Chấp nhận nhưng cách ly để theo dõi.</summary>
    public const string AcceptQuarantine = "ACCEPT_QUARANTINE";
    /// <summary>Từ chối, trả lại nhà cung cấp.</summary>
    public const string RejectReturn     = "REJECT_RETURN";

    // ── Storage (kiểm tra định kỳ trong kho) ──────────────────────────────────
    /// <summary>Giữ nguyên vị trí lưu kho.</summary>
    public const string KeepStored       = "KEEP_STORED";
    /// <summary>Chuyển sang khu cách ly.</summary>
    public const string Quarantine       = "QUARANTINE";

    // ── Recheck (kiểm tra lại sau cách ly) ────────────────────────────────────
    /// <summary>Giải phóng cách ly, cho phép xuất bán.</summary>
    public const string Release          = "RELEASE";
    /// <summary>Tiếp tục giữ cách ly.</summary>
    public const string KeepQuarantine   = "KEEP_QUARANTINE";

    // ── Outbound exception (kiểm tra ngoại lệ khi xuất hàng) ─────────────────
    // Dùng chung RELEASE và QUARANTINE (đã khai báo ở trên)
}

/// <summary>
/// Loại kiểm tra chất lượng — gán vào QualityInspection.InspectionType.
/// Dữ liệu cũ (legacy) để NULL, hiển thị "LEGACY" ở DTO.
/// </summary>
public static class InspectionTypeConstants
{
    public const string Receiving         = "RECEIVING";
    public const string Storage           = "STORAGE";
    public const string Recheck           = "RECHECK";
    public const string OutboundException = "OUTBOUND_EXCEPTION";
}
