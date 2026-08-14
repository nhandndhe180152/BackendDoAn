using System.Collections.Generic;

namespace Backend.Application.DTOs.StockTakes;

public class SaveStockTakeCountsDto
{
    public string? Note { get; set; }
    public List<SaveStockTakeCountItemDto> Items { get; set; } = new();
}

public class SaveStockTakeCountItemDto
{
    public int Id { get; set; }

    /// <summary>
    /// Tổng kg đếm được của dòng. Khi có [Bags], server tự tính lại từ các bao
    /// và BỎ QUA giá trị này — chỉ giữ cho client cũ (kiểm kê thuần kg).
    /// </summary>
    public decimal? ActualQuantity { get; set; }

    public string? Note { get; set; }
    public bool QRScanned { get; set; }
    public bool RecountConfirmed { get; set; }

    /// <summary>Tình trạng chất lượng của dòng: OK / WET / PEST / TORN_BAG / OTHER.</summary>
    public string? QualityStatus { get; set; }

    public string? QualityNote { get; set; }
    public string? QualityImageUrls { get; set; }

    /// <summary>
    /// Kết quả kiểm đếm từng bao. Null = client không kiểm theo bao (dữ liệu bao
    /// của dòng giữ nguyên); mảng rỗng = đã kiểm và KHÔNG thấy bao nào.
    /// </summary>
    public List<SaveStockTakeBagDto>? Bags { get; set; }
}

/// <summary>Một bao được kiểm đếm (quét QR hoặc tích tay).</summary>
public class SaveStockTakeBagDto
{
    /// <summary>Id dòng StockTakeItemBag đã có trong snapshot (0 nếu là bao phát sinh).</summary>
    public int Id { get; set; }

    /// <summary>Id bao vật lý — bắt buộc khi [Id] = 0.</summary>
    public int? PaddyLotBagId { get; set; }

    /// <summary>Mã QR quét được; server dùng để tra bao khi không có Id.</summary>
    public string? QrCode { get; set; }

    public bool Counted { get; set; }
    public bool ScannedByQr { get; set; }

    /// <summary>Null = không cân bao này, giữ nguyên kg sổ sách.</summary>
    public decimal? CountedWeightKg { get; set; }

    public string? QualityStatus { get; set; }
    public string? Note { get; set; }
}

public class SubmitStockTakeDto
{
    public string? Note { get; set; }
}

/// <summary>Kết quả tra một mã QR bao trong lúc kiểm kê (POST .../scan-bag).</summary>
public class ScanStockTakeBagDto
{
    /// <summary>Mã QR đọc được từ tem bao.</summary>
    public string QrCode { get; set; } = string.Empty;
}

public class ScanStockTakeBagResultDto
{
    /// <summary>Bao có thuộc phiếu này không.</summary>
    public bool Matched { get; set; }

    /// <summary>Lý do khi không khớp: NOT_FOUND / OTHER_STOCKTAKE / ALREADY_COUNTED / OUT_OF_SCOPE.</summary>
    public string? Reason { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Dòng kiểm kê chứa bao (null khi không khớp).</summary>
    public int? StockTakeItemId { get; set; }

    public StockTakeItemBagDto? Bag { get; set; }

    /// <summary>Thông tin bao vật lý để hiển thị dù bao nằm ngoài phạm vi phiếu.</summary>
    public string? LotCode { get; set; }
    public string? LocationCode { get; set; }
    public string? ProductVariantName { get; set; }
}
