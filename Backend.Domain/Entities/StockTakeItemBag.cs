using System;
using Backend.Domain.Abstractions;

namespace Backend.Domain.Entities;

/// <summary>
/// Một BAO trong phiếu kiểm kê: ảnh chụp bao lúc lập phiếu + kết quả kiểm đếm.
/// </summary>
/// <remarks>
/// Kho gạo lưu hàng theo bao nên đơn vị kiểm kê phải là bao, không phải kg.
/// Đếm theo kg thì "thiếu 50 kg" không phân biệt được **mất nguyên một bao**
/// (chuyện an ninh kho) với **hao đều nhiều bao** (bay hơi, rơi vãi) — hai việc
/// xử lý hoàn toàn khác nhau.
///
/// Mỗi dòng ứng với một <see cref="PaddyLotBag"/> có thật, trừ bao "phát sinh"
/// (quét được bao không nằm trong snapshot) thì <see cref="PaddyLotBagId"/> vẫn
/// trỏ tới bao đó nhưng <see cref="IsUnexpected"/> = true.
/// </remarks>
public class StockTakeItemBag : EntityAuditBase<int>
{
    public int StockTakeItemId { get; set; }

    public int PaddyLotBagId { get; set; }

    /// <summary>Số thứ tự bao trong lô — chép lại để hiển thị khi bao đã bị xoá.</summary>
    public int BagNo { get; set; }

    /// <summary>Mã QR dán trên bao tại thời điểm chụp phiếu.</summary>
    public string? QrCode { get; set; }

    /// <summary>Khối lượng sổ sách của bao khi chụp phiếu.</summary>
    public decimal SystemWeightKg { get; set; }

    /// <summary>
    /// Khối lượng cân được. Null = không cân bao này (được phép) → khi duyệt
    /// giữ nguyên khối lượng sổ sách, chỉ những bao ĐÃ cân mới bị chỉnh.
    /// </summary>
    public decimal? CountedWeightKg { get; set; }

    /// <summary>Đã tìm thấy bao này khi kiểm kê hay chưa.</summary>
    public bool Counted { get; set; }

    /// <summary>true = quét QR, false = tích tay (tem rách/mờ).</summary>
    public bool ScannedByQr { get; set; }

    /// <summary>Bao đếm được nhưng KHÔNG có trong snapshot (xếp nhầm cột…).</summary>
    public bool IsUnexpected { get; set; }

    /// <summary>Tình trạng chất lượng riêng của bao: OK / WET / PEST / TORN_BAG / OTHER.</summary>
    public string QualityStatus { get; set; } = "OK";

    public string? Note { get; set; }

    public DateTime? CountedAt { get; set; }
    public int? CountedByUserId { get; set; }

    // Computed — không lưu DB
    /// <summary>Chênh lệch kg của riêng bao này (0 khi không cân).</summary>
    public decimal WeightDifference =>
        CountedWeightKg.HasValue ? CountedWeightKg.Value - SystemWeightKg : 0m;

    /// <summary>Bao có trong sổ nhưng không tìm thấy khi kiểm kê.</summary>
    public bool IsMissing => !Counted && !IsUnexpected;

    public virtual StockTakeItem StockTakeItem { get; set; } = null!;
    public virtual PaddyLotBag PaddyLotBag { get; set; } = null!;
    public virtual User? CountedByUser { get; set; }
}
