namespace Backend.Domain.DTParameters;

/// <summary>
/// Bộ lọc cho tổng hợp KPI tồn kho (POST /inventories/summary).
/// Khớp với bộ lọc của bảng để 5 thẻ và bảng luôn đồng bộ.
/// </summary>
/// <remarks>
/// Trước đây chỉ nhận kho/vị trí/danh mục/loại lô, nên khi người dùng bật lọc
/// "Cách ly" hay "Tồn thấp" hoặc gõ từ khoá thì BẢNG đổi còn 5 THẺ đứng yên —
/// nhìn như số liệu sai. Các trường dưới đây đặt tên trùng
/// <see cref="InventoryDTParameters"/> để hai đầu áp cùng một điều kiện.
/// Tất cả đều tuỳ chọn: client cũ không gửi thì hành vi không đổi.
/// </remarks>
public class InventorySummaryParameters
{
    public int? WarehouseId { get; set; }

    public int? LocationId { get; set; }

    public int? ProductCategoryId { get; set; }

    /// <summary>Lọc theo loại lô: PADDY | RICE | BYPRODUCT | PURCHASED_GOOD.</summary>
    public string? LotType { get; set; }

    public int? ProductVariantId { get; set; }

    /// <summary>Lọc theo trạng thái lô (LotStatus.Id) — ví dụ "Đang cách ly".</summary>
    public int? LotStatusId { get; set; }

    /// <summary>true = chỉ lấy tồn còn theo dõi lô (có PaddyLotId).</summary>
    public bool? WithLotOnly { get; set; }

    /// <summary>Lọc theo trạng thái tồn: QUARANTINED | SELLABLE | OTHER_BLOCKED.</summary>
    public string? InventoryState { get; set; }

    /// <summary>true = chỉ hàng đang cách ly; false = chỉ hàng KHÔNG cách ly.</summary>
    public bool? IsQuarantined { get; set; }

    /// <summary>true = chỉ dòng dưới mức tồn tối thiểu.</summary>
    public bool? LowStockOnly { get; set; }

    /// <summary>
    /// Từ khoá chung (mã lô / SKU / tên hàng / kho / vị trí) — cùng cách so khớp
    /// với <c>search.value</c> của bảng.
    /// </summary>
    public string? Search { get; set; }
}
