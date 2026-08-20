using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Common;
using Backend.Application.Constants;
using Backend.Application.DTOs.StockTakes;
using Backend.Domain.Entities;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

/// <summary>
/// Phần kiểm kê THEO BAO của <see cref="StockTakeService"/>.
///
/// Nguyên tắc:
/// • Đơn vị kiểm kê là BAO, không phải kg. kg chỉ là hệ quả của việc cân từng bao.
/// • Lấy hàng ra kiểm theo cột: từ TRÊN xuống DƯỚI (PickSequence 1 = bao trên cùng);
///   cất lại theo chiều ngược lại — bao lấy ra sau cùng được cất vào cột trước
///   (RestowSequence 1 = bao đầu tiên được cất lại).
/// • Bao có vấn đề chất lượng → chuyển sang ô CÁCH LY; bao hỏng → bỏ nguyên bao;
///   kiểm kê lại ô cách ly, bao đạt → rút ra cất về cột thường.
/// </summary>
public partial class StockTakeService
{
    private const decimal BagToleranceKg = 0.001m;

    // ─────────────────────────────────────────────────────────────────────────
    // 1. CHỤP ẢNH BAO KHI LẬP PHIẾU
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sinh ảnh chụp từng bao cho các dòng của phiếu.
    /// PickSequence/RestowSequence tính theo TOÀN CỘT (kể cả bao ngoài phạm vi lô),
    /// vì thủ kho vẫn phải dỡ cả cột từ trên xuống mới lấy được bao ở dưới.
    /// </summary>
    private async Task SnapshotBagsAsync(StockTake stockTake, int? userId)
    {
        var items = stockTake.StockTakeItems.Where(x => !x.IsDeleted && x.LocationId.HasValue).ToList();
        if (items.Count == 0) return;

        var locationIds = items.Select(x => x.LocationId!.Value).Distinct().ToList();
        var now = DateTimeHelper.VietnamNow();

        var bagsByLocation = await _context.PaddyLotBags
            .AsNoTracking()
            .Include(x => x.Lot)
            .Where(x => !x.IsDeleted
                        && x.Status == PaddyLotBagStatuses.Stored
                        && x.LocationId.HasValue
                        && locationIds.Contains(x.LocationId.Value))
            .ToListAsync();

        foreach (var locationId in locationIds)
        {
            // Thứ tự dỡ cột: StackOrder lớn = nằm trên → lấy trước.
            var columnBags = bagsByLocation
                .Where(x => x.LocationId == locationId)
                .OrderByDescending(x => x.StackOrder)
                .ThenByDescending(x => x.Id)
                .ToList();
            var total = columnBags.Count;

            for (int i = 0; i < total; i++)
            {
                var phys = columnBags[i];
                var pickSequence = i + 1;

                var item = items.FirstOrDefault(x =>
                    x.LocationId == locationId &&
                    x.PaddyLotId == phys.LotId);

                // Bao thuộc lô ngoài phạm vi phiếu (ví dụ phiếu kiểm kê theo LÔ) → bỏ qua,
                // nhưng vẫn đã tính vào PickSequence của cột nên thứ tự dỡ vẫn đúng thực tế.
                if (item == null) continue;

                item.Bags.Add(new StockTakeItemBag
                {
                    PaddyLotBagId    = phys.Id,
                    BagNo            = phys.BagNo,
                    QrCode           = phys.QrCode,
                    SystemWeightKg   = phys.WeightKg,
                    SystemStackOrder = phys.StackOrder,
                    PickSequence     = pickSequence,
                    RestowSequence   = total - pickSequence + 1,
                    Counted          = false,
                    Disposition      = StockTakeBagDispositions.Keep,
                    CreatedDate      = now,
                    CreatedBy        = userId
                });
            }
        }

        foreach (var item in items)
            item.SystemBagCount = item.Bags.Count(x => !x.IsDeleted);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. TÍNH LẠI TỔNG CỦA DÒNG TỪ CÁC BAO
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Backend tự tính lại số bao và tổng kg của dòng từ danh sách bao.
    /// Không tin số tổng do client gửi lên.
    /// </summary>
    private static void RecomputeItemTotals(StockTakeItem item)
    {
        var bags = item.Bags.Where(x => !x.IsDeleted).ToList();
        if (bags.Count == 0) return; // dòng dữ liệu cũ chưa quản lý theo bao — giữ nguyên nhập tay

        var touched = bags.Any(x => x.Counted || x.CountedWeightKg.HasValue || x.IsUnexpected);
        if (!touched)
        {
            item.CountedBagCount = null;
            item.ActualQuantity = null;
            return;
        }

        item.CountedBagCount = bags.Count(x => x.Counted);
        item.ActualQuantity = item.AdjustedWeightKg ?? bags.Sum(x => x.EffectiveWeightKg);
        if (item.AdjustedBagCount.HasValue)
            item.CountedBagCount = item.AdjustedBagCount;
    }

    /// <summary>Kg còn LẠI tại vị trí sau khi trừ bao mất/hỏng/chuyển đi.</summary>
    private static decimal RemainingWeightAtLocation(StockTakeItem item) =>
        item.Bags.Where(x => !x.IsDeleted && x.StaysAtLocation).Sum(x => x.EffectiveWeightKg);

    /// <summary>
    /// Lệch SỐ BAO luôn là LARGE dù chênh kg nhỏ: mất nguyên một bao là sự cố an ninh kho,
    /// trong khi theo ngưỡng kg/% một bao 50 kg trong cột 5 tấn chỉ là 1% → trôi qua khâu duyệt.
    /// </summary>
    private static string ApplyBagVarianceSeverity(StockTakeItem item, string severityByWeight)
        => item.HasBagVariance ? "LARGE" : severityByWeight;

    // ─────────────────────────────────────────────────────────────────────────
    // 3. QUÉT QR MỘT BAO TRONG LÚC KIỂM KÊ
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<ApiResponse> ScanBagAsync(int id, ScanStockTakeBagDto dto, int userId)
    {
        var draftId = Lookup.StockTakeStatusId(LookupCodes.StockTakeStatus.Draft);
        var stockTake = await _stockTakeRepository
            .FindByCondition(x => !x.IsDeleted && x.Id == id)
            .Include(x => x.StockTakeItems).ThenInclude(x => x.Bags)
            .FirstOrDefaultAsync();

        if (stockTake == null) return ApiResponse.NotFound();
        if (stockTake.StockTakeStatusId != draftId)
            return ApiResponse.UnprocessableEntity(
                "Chỉ quét bao được khi phiếu đang ở trạng thái Nháp/Đang kiểm.",
                ApiCodeConstants.Common.UnprocessableEntity);

        var qr = dto.QrCode?.Trim();
        if (string.IsNullOrWhiteSpace(qr) && !dto.PaddyLotBagId.HasValue)
            return ApiResponse.BadRequest(message: "Cần mã QR hoặc Id bao để quét.");

        var phys = await _context.PaddyLotBags
            .Include(x => x.Lot)
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => !x.IsDeleted &&
                (dto.PaddyLotBagId.HasValue ? x.Id == dto.PaddyLotBagId.Value : x.QrCode == qr));

        // Quét nhầm bao là chuyện thường ngoài kho — luôn trả 200 kèm lý do
        // để màn hình nói được "bao này thuộc lô/cột nào" thay vì chỉ báo lỗi.
        if (phys == null)
            return ApiResponse.Success(new ScanStockTakeBagResultDto
            {
                Matched = false,
                Reason = "NOT_FOUND",
                Message = "Không tìm thấy bao ứng với mã QR vừa quét."
            });

        var existing = stockTake.StockTakeItems
            .Where(x => !x.IsDeleted)
            .SelectMany(x => x.Bags.Where(b => !b.IsDeleted).Select(b => new { Item = x, Bag = b }))
            .FirstOrDefault(x => x.Bag.PaddyLotBagId == phys.Id);

        var now = DateTimeHelper.VietnamNow();

        if (existing != null)
        {
            if (existing.Bag.Counted)
                return ApiResponse.Success(new ScanStockTakeBagResultDto
                {
                    Matched = true,
                    Reason = "ALREADY_COUNTED",
                    Message = $"Bao #{phys.BagNo} đã được kiểm đếm trước đó.",
                    StockTakeItemId = existing.Item.Id,
                    StockTakeItemBagId = existing.Bag.Id,
                    PaddyLotBagId = phys.Id,
                    BagNo = phys.BagNo,
                    LotCode = phys.Lot?.LotCode,
                    ZoneName = phys.Location?.ZoneName,
                    LocationCode = phys.Location?.SlotCode,
                    SystemWeightKg = existing.Bag.SystemWeightKg,
                    CountedWeightKg = existing.Bag.CountedWeightKg,
                    PickSequence = existing.Bag.PickSequence,
                    RestowSequence = existing.Bag.RestowSequence
                });

            existing.Bag.Counted = true;
            existing.Bag.ScannedByQr = true;
            if (dto.CountedWeightKg.HasValue) existing.Bag.CountedWeightKg = dto.CountedWeightKg;
            existing.Bag.LastModifiedDate = now;
            existing.Bag.UpdatedBy = userId;
            existing.Item.QRScanned = true;
            RecomputeItemTotals(existing.Item);
            await _stockTakeRepository.SaveChangesAsync();

            return ApiResponse.Success(new ScanStockTakeBagResultDto
            {
                Matched = true,
                Reason = "OK",
                Message = $"Đã kiểm đếm bao #{phys.BagNo}.",
                StockTakeItemId = existing.Item.Id,
                StockTakeItemBagId = existing.Bag.Id,
                PaddyLotBagId = phys.Id,
                BagNo = phys.BagNo,
                LotCode = phys.Lot?.LotCode,
                ZoneName = phys.Location?.ZoneName,
                LocationCode = phys.Location?.SlotCode,
                SystemWeightKg = existing.Bag.SystemWeightKg,
                CountedWeightKg = existing.Bag.CountedWeightKg,
                PickSequence = existing.Bag.PickSequence,
                RestowSequence = existing.Bag.RestowSequence
            });
        }

        // Bao PHÁT SINH: không có trong ảnh chụp. Chỉ kéo vào được khi phiếu
        // có dòng đúng lô + đúng vị trí đang kiểm, ngược lại báo ngoài phạm vi.
        var target = stockTake.StockTakeItems.FirstOrDefault(x =>
            !x.IsDeleted && x.PaddyLotId == phys.LotId &&
            (x.LocationId == phys.LocationId || phys.LocationId == null));

        if (target == null)
            return ApiResponse.Success(new ScanStockTakeBagResultDto
            {
                Matched = false,
                Reason = "OUT_OF_SCOPE",
                Message = $"Bao #{phys.BagNo} (lô {phys.Lot?.LotCode}) không thuộc phạm vi phiếu kiểm kê này.",
                PaddyLotBagId = phys.Id,
                BagNo = phys.BagNo,
                LotCode = phys.Lot?.LotCode,
                ZoneName = phys.Location?.ZoneName,
                LocationCode = phys.Location?.SlotCode
            });

        var maxPick = target.Bags.Where(x => !x.IsDeleted).Select(x => x.PickSequence).DefaultIfEmpty(0).Max();
        var unexpected = new StockTakeItemBag
        {
            StockTakeItemId  = target.Id,
            PaddyLotBagId    = phys.Id,
            BagNo            = phys.BagNo,
            QrCode           = phys.QrCode,
            SystemWeightKg   = 0m,                 // sổ sách không có bao này
            SystemStackOrder = phys.StackOrder,
            PickSequence     = maxPick + 1,
            RestowSequence   = 1,                  // bao phát sinh cất lại xuống đáy cột
            Counted          = true,
            ScannedByQr      = true,
            CountedWeightKg  = dto.CountedWeightKg ?? phys.WeightKg,
            IsUnexpected     = true,
            Disposition      = StockTakeBagDispositions.Keep,
            CreatedDate      = now,
            CreatedBy        = userId
        };
        target.Bags.Add(unexpected);
        target.QRScanned = true;
        RecomputeItemTotals(target);
        await _stockTakeRepository.SaveChangesAsync();

        return ApiResponse.Success(new ScanStockTakeBagResultDto
        {
            Matched = true,
            Reason = "PULLED_IN",
            Message = $"Bao #{phys.BagNo} phát sinh ngoài sổ sách — đã thêm vào phiếu.",
            StockTakeItemId = target.Id,
            StockTakeItemBagId = unexpected.Id,
            PaddyLotBagId = phys.Id,
            BagNo = phys.BagNo,
            LotCode = phys.Lot?.LotCode,
            ZoneName = phys.Location?.ZoneName,
            LocationCode = phys.Location?.SlotCode,
            SystemWeightKg = 0m,
            CountedWeightKg = unexpected.CountedWeightKg,
            PickSequence = unexpected.PickSequence,
            RestowSequence = unexpected.RestowSequence,
            IsUnexpected = true
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. CHỌN PHẠM VI: DROPDOWN KHU / CỘT / LÔ + QUÉT QR
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Nguồn dữ liệu cho dropdown chọn CỘT cần kiểm kê.
    ///
    /// Lấy theo TỒN KHO (Inventory) — đúng nguồn mà <see cref="CreateAsync"/> dùng
    /// để dựng dòng phiếu — rồi hợp nhất thêm cột đang có bao. Trước đây chỉ liệt kê
    /// cột có PaddyLotBag nên bỏ sót gần hết: dữ liệu cũ nhập kho khi chưa quản lý
    /// theo bao chỉ có dòng Inventory, không có bao nào.
    /// </summary>
    public async Task<ApiResponse> GetScopeOptionsAsync(int warehouseId, bool? quarantineOnly)
    {
        var inventoryRows = await _context.Inventories.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.WarehouseId == warehouseId
                        && x.LocationId != null
                        && x.QuantityOnHand > 0)
            .Select(x => new { LocationId = x.LocationId!.Value, x.QuantityOnHand })
            .ToListAsync();

        var bagRows = await _context.PaddyLotBags.AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.Status == PaddyLotBagStatuses.Stored
                        && x.LocationId != null
                        && x.Location != null
                        && x.Location.WarehouseId == warehouseId)
            .Select(x => new { LocationId = x.LocationId!.Value, x.WeightKg })
            .ToListAsync();

        var locationIds = inventoryRows.Select(x => x.LocationId)
            .Concat(bagRows.Select(x => x.LocationId))
            .Distinct()
            .ToList();
        if (locationIds.Count == 0)
            return ApiResponse.Success(new StockTakeScopeOptionsDto());

        var locations = await _context.Locations.AsNoTracking()
            .Where(x => !x.IsDeleted && !x.IsOutboundStaging && locationIds.Contains(x.Id))
            .ToListAsync();

        if (quarantineOnly == true) locations = locations.Where(x => x.IsQuarantine).ToList();
        else if (quarantineOnly == false) locations = locations.Where(x => !x.IsQuarantine).ToList();

        var inventoryByLocation = inventoryRows
            .GroupBy(x => x.LocationId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.QuantityOnHand));
        var bagsByLocation = bagRows
            .GroupBy(x => x.LocationId)
            .ToDictionary(g => g.Key, g => new { Count = g.Count(), Kg = g.Sum(x => x.WeightKg) });

        var result = new StockTakeScopeOptionsDto
        {
            Columns = locations
                .Select(loc =>
                {
                    var hasBags = bagsByLocation.TryGetValue(loc.Id, out var bags);
                    return new StockTakeColumnOptionDto
                    {
                        LocationId = loc.Id,
                        ZoneName = loc.ZoneName,
                        LocationCode = loc.SlotCode,
                        QrCode = loc.QrCode,
                        IsQuarantine = loc.IsQuarantine,
                        BagCount = hasBags ? bags!.Count : 0,
                        // Ưu tiên kg theo tồn kho: đó mới là con số phiếu kiểm kê
                        // đối chiếu; kg trong bao chỉ dùng khi chưa có dòng tồn.
                        TotalWeightKg = inventoryByLocation.TryGetValue(loc.Id, out var kg)
                            ? kg
                            : (hasBags ? bags!.Kg : 0m)
                    };
                })
                .OrderBy(x => x.ZoneName).ThenBy(x => x.LocationCode)
                .ToList()
        };

        return ApiResponse.Success(result);
    }

    /// <summary>
    /// Quét QR dán trên CỘT để chọn nhanh phạm vi kiểm kê.
    ///
    /// Tem QR mang payload <c>STOCKLITE|{WarehouseId}|LOCATION|{QrCode}</c>
    /// (xem QRCodeService) nên phải tách lấy phần mã ở cuối.
    ///
    /// KHÔNG lọc theo kho khi tìm: quét đúng tem mà app đang mở kho khác thì báo
    /// "không khớp" là sai và rất khó hiểu — cứ trả về cột kèm kho của nó để màn
    /// hình tự chuyển kho theo.
    /// </summary>
    public async Task<ApiResponse> ResolveScopeQrAsync(string qrCode, int? warehouseId)
    {
        var raw = qrCode?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return ApiResponse.BadRequest(message: "Mã QR không được để trống.");

        var (entityType, code) = ParseQrPayload(raw);

        if (entityType != null && entityType != "LOCATION")
            return ApiResponse.Success(new StockTakeScopeResolveDto
            {
                Matched = false,
                Message = entityType == "BAG"
                    ? "Đây là tem của một BAO. Kiểm kê chọn phạm vi bằng tem dán trên CỘT."
                    : "Tem này không phải tem cột. Vui lòng quét tem QR dán trên cột."
            });

        var location = await _context.Locations.AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.QrCode == code);

        // Máy quét/bàn phím có thể trả mã lẫn hoa-thường; so lại một lần nữa
        // trước khi kết luận là không khớp.
        location ??= await _context.Locations.AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.QrCode != null
                                      && x.QrCode.ToLower() == code.ToLower());

        if (location == null)
            return ApiResponse.Success(new StockTakeScopeResolveDto
            {
                Matched = false,
                Message = $"Mã QR '{code}' không khớp cột nào trong hệ thống."
            });

        if (location.IsOutboundStaging)
            return ApiResponse.Success(new StockTakeScopeResolveDto
            {
                Matched = false,
                Message = "Khu chờ xuất không kiểm kê được vì hàng đang gắn với phiếu xuất."
            });

        var bagCount = await _context.PaddyLotBags.AsNoTracking()
            .CountAsync(x => !x.IsDeleted && x.Status == PaddyLotBagStatuses.Stored
                             && x.LocationId == location.Id);
        var stockKg = await _context.Inventories.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LocationId == location.Id)
            .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;

        var columnName = location.SlotCode ?? location.Id.ToString();
        var message = warehouseId.HasValue && warehouseId.Value != location.WarehouseId
            ? $"Cột {columnName} — khu {location.ZoneName} (thuộc kho khác, đã tự chuyển kho)."
            : $"Cột {columnName} — khu {location.ZoneName}.";

        return ApiResponse.Success(new StockTakeScopeResolveDto
        {
            Matched = true,
            ScopeType = ScopeTypes.Column,
            Message = message,
            ZoneName = location.ZoneName,
            LocationId = location.Id,
            LocationCode = location.SlotCode,
            WarehouseId = location.WarehouseId,
            IsQuarantine = location.IsQuarantine,
            BagCount = bagCount,
            TotalWeightKg = stockKg
        });
    }

    /// <summary>
    /// Tách payload tem QR. Trả về (null, chuỗi gốc) khi không đúng định dạng
    /// STOCKLITE — coi như người dùng gõ thẳng mã của cột.
    ///
    /// Có bước giải mã %xx: payload chứa ký tự '|', đi qua query string mà lỡ bị
    /// encode hai lần thì chuỗi tới nơi vẫn còn %7C và không tách ra được.
    /// </summary>
    private static (string? EntityType, string Code) ParseQrPayload(string raw)
    {
        var value = raw;
        if (value.Contains('%'))
        {
            try { value = Uri.UnescapeDataString(value).Trim(); }
            catch (UriFormatException) { /* giữ nguyên chuỗi gốc */ }
        }

        var parts = value.Split('|');
        if (parts.Length == 4 && string.Equals(parts[0], "STOCKLITE", StringComparison.OrdinalIgnoreCase))
            return (parts[2].Trim().ToUpperInvariant(), parts[3].Trim());
        return (null, value);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 5. GỢI Ý VỊ TRÍ ĐÍCH CHO BAO (ô cách ly / cột thường)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Gợi ý vị trí đích cho một bao khi chuyển cách ly (QUARANTINE) hoặc rút khỏi
    /// cách ly về khu thường (RELEASE). Gợi ý theo cùng cách với xếp kho ở lệnh xay xát:
    /// còn đủ chỗ, ưu tiên cột đang chứa cùng SKU rồi tới cột trống, người dùng chọn lại được.
    /// </summary>
    public async Task<ApiResponse> GetBagTargetSuggestionsAsync(int stockTakeId, int stockTakeItemBagId)
    {
        var bag = await _context.StockTakeItemBags.AsNoTracking()
            .Include(x => x.PaddyLotBag).ThenInclude(x => x.Lot)
            .Include(x => x.StockTakeItem).ThenInclude(x => x.StockTake)
            .Include(x => x.StockTakeItem).ThenInclude(x => x.Location)
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == stockTakeItemBagId
                                      && x.StockTakeItem.StockTakeId == stockTakeId);
        if (bag == null) return ApiResponse.NotFound();

        // Bao chưa chọn cách xử lý (KEEP) vẫn cần gợi ý sẵn để bấm "Chuyển cách ly" là dùng được ngay:
        // phiếu thường gợi ý ô CÁCH LY, phiếu kiểm kê ô cách ly gợi ý cột THƯỜNG.
        var wantQuarantine = bag.Disposition switch
        {
            StockTakeBagDispositions.Release => false,
            StockTakeBagDispositions.Quarantine => true,
            _ => !(bag.StockTakeItem.StockTake?.IsQuarantineScope ?? false)
        };

        var suggestions = await BuildTargetSuggestionsAsync(
            bag.StockTakeItem.StockTake!.WarehouseId,
            bag.PaddyLotBag.Lot?.ProductVariantId,
            bag.StockTakeItem.LocationId,
            bag.CountedWeightKg ?? bag.SystemWeightKg,
            wantQuarantine);

        return ApiResponse.Success(suggestions);
    }

    private async Task<List<StockTakeBagTargetSuggestionDto>> BuildTargetSuggestionsAsync(
        int warehouseId, int? productVariantId, int? excludeLocationId, decimal bagWeightKg, bool wantQuarantine)
    {
        var locations = await _context.Locations.AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive && x.WarehouseId == warehouseId
                        && !x.IsOutboundStaging && x.IsQuarantine == wantQuarantine
                        && (!excludeLocationId.HasValue || x.Id != excludeLocationId.Value))
            .ToListAsync();
        if (locations.Count == 0) return new List<StockTakeBagTargetSuggestionDto>();

        var locationIds = locations.Select(x => x.Id).ToList();
        var occupancy = await _context.Inventories.AsNoTracking()
            .Where(x => !x.IsDeleted && x.LocationId.HasValue && locationIds.Contains(x.LocationId.Value))
            .GroupBy(x => x.LocationId!.Value)
            .Select(g => new { LocationId = g.Key, Kg = g.Sum(x => x.QuantityOnHand) })
            .ToDictionaryAsync(x => x.LocationId, x => x.Kg);

        var result = new List<StockTakeBagTargetSuggestionDto>();
        foreach (var loc in locations)
        {
            var used = occupancy.TryGetValue(loc.Id, out var kg) ? kg : 0m;
            var capacity = loc.MaxCapacity ?? 0m;
            var available = capacity > 0 ? capacity - used : decimal.MaxValue / 2;

            // Không đủ chỗ cho chính bao đang xử lý thì không phải là gợi ý.
            if (capacity > 0 && available < bagWeightKg) continue;

            decimal score = 0m;
            var reasons = new List<string>();

            if (productVariantId.HasValue && loc.CurrentProductVariantId == productVariantId.Value)
            {
                score += 50m;
                reasons.Add("cùng loại hàng đang chứa");
            }
            else if (used <= BagToleranceKg)
            {
                score += 30m;
                reasons.Add("cột trống");
            }
            else if (loc.IsSingleTypeColumn && loc.CurrentProductVariantId.HasValue)
            {
                // Cột một-loại đang chứa SKU khác → không xếp chung được.
                continue;
            }

            if (capacity > 0)
            {
                var freeRatio = Math.Max(0m, Math.Min(1m, available / capacity));
                score += freeRatio * 30m;
                reasons.Add($"còn trống {available:0.#} kg");
            }

            score += Math.Max(0, 20 - loc.Priority);

            result.Add(new StockTakeBagTargetSuggestionDto
            {
                LocationId = loc.Id,
                ZoneName = loc.ZoneName,
                LocationCode = loc.SlotCode,
                IsQuarantine = loc.IsQuarantine,
                MaxCapacityKg = capacity,
                CurrentOccupancyKg = used,
                AvailableKg = capacity > 0 ? available : 0m,
                Score = Math.Round(score, 2),
                Reason = reasons.Count > 0 ? string.Join(", ", reasons) : "còn chỗ"
            });
        }

        var ordered = result.OrderByDescending(x => x.Score).ThenBy(x => x.LocationCode).Take(5).ToList();
        if (ordered.Count > 0) ordered[0].IsRecommended = true;
        return ordered;
    }

    /// <summary>Vị trí đích mặc định do backend chọn khi người dùng chưa chỉ định.</summary>
    private async Task<int?> ResolveDefaultTargetLocationAsync(
        int warehouseId, int? productVariantId, int? excludeLocationId, decimal bagWeightKg, bool wantQuarantine)
    {
        var suggestions = await BuildTargetSuggestionsAsync(
            warehouseId, productVariantId, excludeLocationId, bagWeightKg, wantQuarantine);
        return suggestions.FirstOrDefault()?.LocationId;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 6. DUYỆT PHIẾU — ĐỒNG BỘ BAO VẬT LÝ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Khóa một dòng tồn kho cần tính lại sau khi bao thay đổi.</summary>
    private readonly record struct InventoryKey(int ProductVariantId, int? LocationId, int? PaddyLotId);

    private sealed class BagSyncResult
    {
        public HashSet<InventoryKey> TouchedInventories { get; } = new();
        public HashSet<int> TouchedLocations { get; } = new();
        public int MissingBags { get; set; }
        public int DisposedBags { get; set; }
        public int QuarantinedBags { get; set; }
        public int ReleasedBags { get; set; }
    }

    private static void TrackBagKeys(BagSyncResult result, PaddyLotBag phys, int? locationId)
    {
        if (!locationId.HasValue) return;
        result.TouchedLocations.Add(locationId.Value);

        var variantId = phys.Lot?.ProductVariantId;
        var contents = phys.Contents.Where(x => !x.IsDeleted).ToList();
        if (contents.Count == 0)
        {
            if (variantId.HasValue)
                result.TouchedInventories.Add(new InventoryKey(variantId.Value, locationId, phys.LotId));
            return;
        }

        foreach (var content in contents)
        {
            var contentVariantId = content.Lot?.ProductVariantId ?? variantId;
            if (contentVariantId.HasValue)
                result.TouchedInventories.Add(new InventoryKey(contentVariantId.Value, locationId, content.LotId));
        }
    }

    /// <summary>
    /// Chia lại khối lượng các thành phần trong bao theo TỈ LỆ khi cân lại,
    /// dồn phần dư vào thành phần cuối — bất biến bao–tồn chỉ cho sai 0,001 kg
    /// nên không được để sai số làm tròn tích lại.
    /// </summary>
    private static void ScaleBagContents(PaddyLotBag phys, decimal newWeightKg, DateTime now, int userId)
    {
        // Bao dữ liệu cũ chưa có dòng thành phần thì chỉ cần đổi khối lượng bao.
        var contents = phys.Contents.Where(x => !x.IsDeleted).ToList();
        if (contents.Count > 0)
        {
            var oldTotal = contents.Sum(x => x.WeightKg);
            if (oldTotal <= BagToleranceKg)
            {
                contents[0].WeightKg = newWeightKg;
                for (int i = 1; i < contents.Count; i++) contents[i].WeightKg = 0m;
            }
            else
            {
                decimal assigned = 0m;
                for (int i = 0; i < contents.Count - 1; i++)
                {
                    var share = Math.Round(contents[i].WeightKg / oldTotal * newWeightKg, 3, MidpointRounding.AwayFromZero);
                    contents[i].WeightKg = share;
                    assigned += share;
                }
                contents[^1].WeightKg = Math.Max(0m, newWeightKg - assigned);
            }

            foreach (var content in contents)
            {
                content.LastModifiedDate = now;
                content.UpdatedBy = userId;
            }
        }

        phys.WeightKg = newWeightKg;
        phys.IsFull = !phys.StandardWeightKg.HasValue || newWeightKg >= phys.StandardWeightKg.Value;
    }

    private void AddBagMovement(PaddyLotBag phys, string movementType, int? fromLocationId, int? toLocationId,
        decimal before, decimal after, int stockTakeId, int? referenceItemId, string note, int userId, DateTime now)
    {
        _context.PaddyLotBagMovements.Add(new PaddyLotBagMovement
        {
            BagId          = phys.Id,
            MovementType   = movementType,
            FromLocationId = fromLocationId,
            ToLocationId   = toLocationId,
            WeightKg       = Math.Abs(after - before) < BagToleranceKg ? after : Math.Abs(after - before),
            BeforeWeightKg = before,
            AfterWeightKg  = after,
            ReferenceType  = InventoryReferenceTypeConstants.StockTake,
            ReferenceId    = stockTakeId,
            ReferenceItemId = referenceItemId,
            Note           = note,
            CreatedBy      = userId,
            CreatedDate    = now
        });
    }

    /// <summary>
    /// Áp kết quả kiểm kê xuống BAO VẬT LÝ. Chạy TRƯỚC khi điều chỉnh Inventory,
    /// vì tồn kho sau đó được tính lại từ chính khối lượng trong bao — nếu làm ngược
    /// thì bất biến SUM(kg trong bao) == Inventory sẽ vỡ và lần xuất kho kế tiếp báo "lệch tồn".
    /// </summary>
    private async Task<BagSyncResult> SyncPhysicalBagsAsync(StockTake stockTake, int userId, DateTime now)
    {
        var result = new BagSyncResult();
        var pairs = stockTake.StockTakeItems
            .Where(x => !x.IsDeleted)
            .SelectMany(item => item.Bags.Where(b => !b.IsDeleted).Select(b => (Item: item, Row: b)))
            .ToList();
        if (pairs.Count == 0) return result;

        var physIds = pairs.Select(x => x.Row.PaddyLotBagId).Distinct().ToList();
        var physBags = await _context.PaddyLotBags
            .Include(x => x.Lot)
            .Include(x => x.Contents).ThenInclude(x => x.Lot)
            .Where(x => physIds.Contains(x.Id))
            .ToListAsync();
        var physMap = physBags.ToDictionary(x => x.Id);

        // Con trỏ đỉnh cột cho từng vị trí đích, để nhiều bao chuyển vào cùng cột
        // không dẫm lên nhau khi gán StackOrder.
        var stackCursor = new Dictionary<int, int>();
        async Task<int> NextStackOrderAsync(int locationId)
        {
            if (!stackCursor.TryGetValue(locationId, out var current))
            {
                current = await _context.PaddyLotBags
                    .Where(x => !x.IsDeleted && x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored)
                    .MaxAsync(x => (int?)x.StackOrder) ?? 0;
            }
            current++;
            stackCursor[locationId] = current;
            return current;
        }

        foreach (var (item, row) in pairs)
        {
            if (!physMap.TryGetValue(row.PaddyLotBagId, out var phys)) continue;

            var fromLocationId = phys.LocationId;
            TrackBagKeys(result, phys, fromLocationId);
            TrackBagKeys(result, phys, item.LocationId);

            // ── (a) Không tìm thấy bao → coi như đã mất khỏi kho ───────────────
            if (!row.Counted)
            {
                var before = phys.WeightKg;
                ScaleBagContents(phys, 0m, now, userId);
                phys.Status = PaddyLotBagStatuses.Consumed;
                phys.LocationId = null;
                phys.StackOrder = 0;
                phys.OpenBagKey = null;
                phys.LastModifiedDate = now;
                phys.UpdatedBy = userId;
                result.MissingBags++;

                AddBagMovement(phys, PaddyLotBagMovementTypes.StockTakeMissing, fromLocationId, null,
                    before, 0m, stockTake.Id, item.Id,
                    $"Kiểm kê {stockTake.STCode}: không tìm thấy bao #{phys.BagNo}", userId, now);
                continue;
            }

            // ── (b) Cân lại ────────────────────────────────────────────────────
            if (row.CountedWeightKg.HasValue &&
                Math.Abs(row.CountedWeightKg.Value - phys.WeightKg) > BagToleranceKg)
            {
                var before = phys.WeightKg;
                ScaleBagContents(phys, row.CountedWeightKg.Value, now, userId);
                phys.LastModifiedDate = now;
                phys.UpdatedBy = userId;

                AddBagMovement(phys, PaddyLotBagMovementTypes.StockTakeAdjust, fromLocationId, fromLocationId,
                    before, phys.WeightKg, stockTake.Id, item.Id,
                    $"Kiểm kê {stockTake.STCode}: cân lại bao #{phys.BagNo}", userId, now);
            }

            // ── (c) Bao hỏng → bỏ nguyên bao ra khỏi kho ───────────────────────
            if (row.Disposition == StockTakeBagDispositions.Dispose)
            {
                var before = phys.WeightKg;
                ScaleBagContents(phys, 0m, now, userId);
                phys.Status = PaddyLotBagStatuses.Disposed;
                phys.LocationId = null;
                phys.StackOrder = 0;
                phys.OpenBagKey = null;
                phys.LastModifiedDate = now;
                phys.UpdatedBy = userId;
                result.DisposedBags++;

                AddBagMovement(phys, PaddyLotBagMovementTypes.StockTakeDispose, fromLocationId, null,
                    before, 0m, stockTake.Id, item.Id,
                    $"Kiểm kê {stockTake.STCode}: bao #{phys.BagNo} hỏng — bỏ cả bao" +
                    (string.IsNullOrWhiteSpace(row.DispositionNote) ? "" : $" ({row.DispositionNote})"),
                    userId, now);
                continue;
            }

            // ── (d) Chuyển cách ly / rút khỏi cách ly ──────────────────────────
            if (row.Disposition == StockTakeBagDispositions.Quarantine ||
                row.Disposition == StockTakeBagDispositions.Release)
            {
                var toLocationId = row.TargetLocationId!.Value;
                phys.LocationId = toLocationId;
                phys.StackOrder = await NextStackOrderAsync(toLocationId);
                phys.OpenBagKey = null;
                phys.BagKind = row.Disposition == StockTakeBagDispositions.Quarantine
                    ? PaddyLotBagKinds.Quarantine
                    : (phys.StandardWeightKg.HasValue ? PaddyLotBagKinds.Finished : PaddyLotBagKinds.Purchase);
                phys.LastModifiedDate = now;
                phys.UpdatedBy = userId;

                if (row.Disposition == StockTakeBagDispositions.Quarantine) result.QuarantinedBags++;
                else result.ReleasedBags++;

                TrackBagKeys(result, phys, toLocationId);

                AddBagMovement(phys,
                    row.Disposition == StockTakeBagDispositions.Quarantine
                        ? PaddyLotBagMovementTypes.StockTakeQuarantine
                        : PaddyLotBagMovementTypes.StockTakeRelease,
                    fromLocationId, toLocationId, phys.WeightKg, phys.WeightKg, stockTake.Id, item.Id,
                    row.Disposition == StockTakeBagDispositions.Quarantine
                        ? $"Kiểm kê {stockTake.STCode}: chuyển bao #{phys.BagNo} sang ô cách ly"
                        : $"Kiểm kê {stockTake.STCode}: rút bao #{phys.BagNo} khỏi ô cách ly, cất về cột thường",
                    userId, now);
                continue;
            }

            // ── (e) Bao phát sinh đứng sai chỗ → kéo về vị trí đang kiểm ───────
            if (row.IsUnexpected && item.LocationId.HasValue && phys.LocationId != item.LocationId)
            {
                phys.LocationId = item.LocationId;
                phys.Status = PaddyLotBagStatuses.Stored;
                phys.StackOrder = await NextStackOrderAsync(item.LocationId.Value);
                phys.LastModifiedDate = now;
                phys.UpdatedBy = userId;

                AddBagMovement(phys, PaddyLotBagMovementTypes.StockTakeFound, fromLocationId, item.LocationId,
                    phys.WeightKg, phys.WeightKg, stockTake.Id, item.Id,
                    $"Kiểm kê {stockTake.STCode}: bao #{phys.BagNo} phát sinh — kéo về đúng vị trí", userId, now);
            }
        }

        await _context.SaveChangesAsync(default);
        return result;
    }

    /// <summary>
    /// Xếp lại thứ tự bao trong cột sau khi lấy ra kiểm và cất lại:
    /// bao lấy ra sau cùng (nằm dưới cùng) được cất vào trước, nên trật tự đáy→đỉnh
    /// giữ nguyên, chỉ nén lại các khoảng trống do bao bị rút đi.
    /// Bao MỞ (chưa đầy) luôn bị đẩy lên đỉnh để không vỡ bất biến "bao mở phải ở đỉnh cột".
    /// </summary>
    private async Task RestowColumnsAsync(IEnumerable<int> locationIds, int stockTakeId, string stCode, int userId, DateTime now)
    {
        foreach (var locationId in locationIds.Distinct())
        {
            var bags = await _context.PaddyLotBags
                .Where(x => !x.IsDeleted && x.LocationId == locationId && x.Status == PaddyLotBagStatuses.Stored)
                .ToListAsync();
            if (bags.Count == 0) continue;

            var ordered = bags
                .OrderBy(x => x.IsFull ? 0 : 1)   // bao mở lên đỉnh
                .ThenBy(x => x.StackOrder)
                .ThenBy(x => x.Id)
                .ToList();

            for (int i = 0; i < ordered.Count; i++)
            {
                var newOrder = i + 1;
                if (ordered[i].StackOrder == newOrder) continue;

                var oldOrder = ordered[i].StackOrder;
                ordered[i].StackOrder = newOrder;
                ordered[i].LastModifiedDate = now;
                ordered[i].UpdatedBy = userId;

                AddBagMovement(ordered[i], PaddyLotBagMovementTypes.StockTakeRestow, locationId, locationId,
                    ordered[i].WeightKg, ordered[i].WeightKg, stockTakeId, null,
                    $"Kiểm kê {stCode}: cất lại bao #{ordered[i].BagNo} — vị trí xếp {oldOrder} → {newOrder}",
                    userId, now);
            }
        }

        await _context.SaveChangesAsync(default);
    }

    /// <summary>Đồng bộ lại sức chứa cột theo tồn thực tế (self-healing).</summary>
    private async Task SyncLocationOccupancyAsync(IEnumerable<int> locationIds, int userId, DateTime now)
    {
        foreach (var locationId in locationIds.Distinct())
        {
            var location = await _context.Locations.FirstOrDefaultAsync(x => x.Id == locationId && !x.IsDeleted);
            if (location == null) continue;

            location.CurrentOccupancy = await _context.Inventories
                .Where(x => !x.IsDeleted && x.LocationId == locationId)
                .SumAsync(x => (decimal?)x.QuantityOnHand) ?? 0m;

            if (location.CurrentOccupancy <= BagToleranceKg)
            {
                location.CurrentOccupancy = 0m;
                location.CurrentProductVariantId = null;
            }
            else if (!location.CurrentProductVariantId.HasValue)
            {
                // Cột vừa nhận bao chuyển tới: gắn lại loại hàng đang chứa, nếu không
                // quy tắc "cột một loại hàng" ở gợi ý xếp kho sẽ coi cột này là trống.
                location.CurrentProductVariantId = await _context.Inventories
                    .Where(x => !x.IsDeleted && x.LocationId == locationId && x.QuantityOnHand > 0)
                    .OrderByDescending(x => x.QuantityOnHand)
                    .Select(x => (int?)x.ProductVariantId)
                    .FirstOrDefaultAsync();
            }

            location.LastModifiedDate = now;
            location.UpdatedBy = userId;
        }

        await _context.SaveChangesAsync(default);
    }

    /// <summary>
    /// Tổng kg đang nằm trong bao của một (lô, vị trí) — nguồn sự thật để đặt lại Inventory.
    ///
    /// Thành phần bao (PaddyLotBagContent) là nguồn chuẩn vì một bao có thể trộn
    /// nhiều lô. Nhưng dữ liệu CŨ có bao chưa hề sinh dòng content: nếu cứ cộng
    /// content thì tổng ra 0 và bước duyệt sẽ xoá sạch tồn của lô đó. Vì vậy khi
    /// (lô, vị trí) không có content nào, quay về cộng khối lượng bao theo lô đại diện.
    /// </summary>
    private async Task<decimal> SumBagContentWeightAsync(int? paddyLotId, int? locationId)
    {
        if (!locationId.HasValue) return 0m;

        if (paddyLotId.HasValue)
        {
            var hasContents = await _context.PaddyLotBagContents
                .AnyAsync(x => !x.IsDeleted && x.LotId == paddyLotId.Value
                               && x.Bag.LocationId == locationId.Value
                               && x.Bag.Status == PaddyLotBagStatuses.Stored
                               && !x.Bag.IsDeleted);

            if (hasContents)
            {
                return await _context.PaddyLotBagContents
                    .Where(x => !x.IsDeleted && x.LotId == paddyLotId.Value
                                && x.Bag.LocationId == locationId.Value
                                && x.Bag.Status == PaddyLotBagStatuses.Stored
                                && !x.Bag.IsDeleted)
                    .SumAsync(x => (decimal?)x.WeightKg) ?? 0m;
            }

            return await _context.PaddyLotBags
                .Where(x => !x.IsDeleted && x.LotId == paddyLotId.Value
                            && x.LocationId == locationId.Value
                            && x.Status == PaddyLotBagStatuses.Stored)
                .SumAsync(x => (decimal?)x.WeightKg) ?? 0m;
        }

        return await _context.PaddyLotBags
            .Where(x => !x.IsDeleted && x.LocationId == locationId.Value
                        && x.Status == PaddyLotBagStatuses.Stored)
            .SumAsync(x => (decimal?)x.WeightKg) ?? 0m;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 7. LƯU VẾT CHẤT LƯỢNG — SINH PHIẾU KIỂM ĐỊNH TỪ KẾT QUẢ KIỂM KÊ
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Kiểm kê vừa CHUYỂN HÀNG (bao lỗi sang ô cách ly) vừa ghi HỒ SƠ CHẤT LƯỢNG:
    /// mỗi lô có bao được đánh giá sẽ sinh một QualityInspection kèm kết quả từng bao,
    /// để màn Kiểm định và truy vết vẫn thấy đủ lịch sử như khi kiểm định thủ công.
    /// </summary>
    private async Task CreateQualityTraceAsync(StockTake stockTake, int userId, DateTime now)
    {
        var inspectionType = stockTake.IsQuarantineScope
            ? InspectionTypeConstants.Recheck
            : InspectionTypeConstants.Storage;

        var assessed = stockTake.StockTakeItems
            .Where(x => !x.IsDeleted && x.PaddyLotId.HasValue)
            .SelectMany(item => item.Bags
                .Where(b => !b.IsDeleted && b.Counted &&
                            (!string.IsNullOrWhiteSpace(b.QualityResult) ||
                             b.Disposition != StockTakeBagDispositions.Keep))
                .Select(b => (LotId: item.PaddyLotId!.Value, Row: b)))
            .ToList();
        if (assessed.Count == 0) return;

        foreach (var group in assessed.GroupBy(x => x.LotId))
        {
            var rows = group.Select(x => x.Row).ToList();
            var affectedKg = rows
                .Where(x => x.Disposition != StockTakeBagDispositions.Keep ||
                            x.QualityResult == StockTakeQualityResults.IssueDetected)
                .Sum(x => x.EffectiveWeightKg);

            var inspection = new QualityInspection
            {
                PaddyLotId       = group.Key,
                InspectedAt      = now,
                InspectionType   = inspectionType,
                InspectorId      = userId,
                PassedInspection = rows.All(x => x.QualityResult != StockTakeQualityResults.IssueDetected),
                AffectedWeightKg = affectedKg,
                Note             = $"Sinh tự động từ phiếu kiểm kê {stockTake.STCode}.",
                CompletedAt      = now,
                CompletedBy      = userId,
                CreatedDate      = now,
                CreatedBy        = userId
            };
            await _context.QualityInspections.AddAsync(inspection);
            await _context.SaveChangesAsync(default);

            foreach (var row in rows)
            {
                _context.QualityInspectionBagResults.Add(new QualityInspectionBagResult
                {
                    QualityInspectionId = inspection.Id,
                    BagId               = row.PaddyLotBagId,
                    InspectedAt         = now,
                    InspectorId         = userId,
                    MoisturePercent     = row.MoisturePercent,
                    ImpurityPercent     = row.ImpurityPercent,
                    MoldLevel           = row.MoldLevel,
                    PestLevel           = row.PestLevel,
                    PackagingStatus     = row.PackagingStatus,
                    QualityResult       = row.QualityResult,
                    Disposition         = MapQualityDisposition(row.Disposition, stockTake.IsQuarantineScope),
                    Handling            = row.Disposition == StockTakeBagDispositions.Dispose
                        ? "Loại bỏ nguyên bao hỏng khi kiểm kê"
                        : null,
                    Note                = row.QualityNote ?? row.DispositionNote,
                    CreatedDate         = now,
                    CreatedBy           = userId
                });
            }

            await _context.SaveChangesAsync(default);
        }
    }

    /// <summary>
    /// Ánh xạ cách xử lý của kiểm kê sang Disposition của kiểm định.
    /// Bao hỏng bị loại bỏ không có Disposition tương ứng bên kiểm định nên để null
    /// và ghi rõ ở Handling — gán bừa QUARANTINE sẽ làm báo cáo cách ly sai số.
    /// </summary>
    private static string? MapQualityDisposition(string disposition, bool isQuarantineScope) => disposition switch
    {
        StockTakeBagDispositions.Keep => isQuarantineScope
            ? BagDispositionConstants.KeepQuarantine
            : BagDispositionConstants.KeepStored,
        StockTakeBagDispositions.Quarantine => BagDispositionConstants.Quarantine,
        StockTakeBagDispositions.Release => BagDispositionConstants.Release,
        _ => null
    };

    // ─────────────────────────────────────────────────────────────────────────
    // 8. VALIDATE CÁCH XỬ LÝ BAO + TỰ ĐIỀN VỊ TRÍ ĐÍCH
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Chỉ GỢI Ý vị trí đích cho bao cần chuyển mà người dùng chưa chọn — dùng khi lưu nháp.
    /// Không chặn lưu nếu chưa tìm được chỗ; ràng buộc chặt nằm ở bước gửi duyệt/duyệt.
    /// </summary>
    private async Task FillMissingBagTargetsAsync(StockTake stockTake, int userId, DateTime now)
    {
        var items = stockTake.StockTakeItems.Where(x => !x.IsDeleted).ToList();
        var lotIds = items.Where(x => x.PaddyLotId.HasValue).Select(x => x.PaddyLotId!.Value).Distinct().ToList();
        var lotVariants = lotIds.Count == 0
            ? new Dictionary<int, int>()
            : await _context.PaddyLots.AsNoTracking()
                .Where(x => lotIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.ProductVariantId);

        foreach (var item in items)
        {
            foreach (var row in item.Bags.Where(x => !x.IsDeleted && !x.TargetLocationId.HasValue))
            {
                if (row.Disposition != StockTakeBagDispositions.Quarantine &&
                    row.Disposition != StockTakeBagDispositions.Release) continue;

                var variantId = item.PaddyLotId.HasValue && lotVariants.TryGetValue(item.PaddyLotId.Value, out var v)
                    ? v
                    : item.ProductVariantId;

                row.TargetLocationId = await ResolveDefaultTargetLocationAsync(
                    stockTake.WarehouseId, variantId, item.LocationId,
                    row.CountedWeightKg ?? row.SystemWeightKg,
                    row.Disposition == StockTakeBagDispositions.Quarantine);
                row.LastModifiedDate = now;
                row.UpdatedBy = userId;
            }
        }
    }

    /// <summary>
    /// Kiểm tra tính hợp lệ của cách xử lý từng bao và tự điền vị trí đích còn thiếu
    /// bằng gợi ý của backend. Trả về thông báo lỗi; null nghĩa là hợp lệ.
    /// </summary>
    private async Task<string?> ValidateAndFillBagDispositionsAsync(StockTake stockTake, int userId, DateTime now)
    {
        var items = stockTake.StockTakeItems.Where(x => !x.IsDeleted).ToList();
        var locationIds = items.Where(x => x.LocationId.HasValue).Select(x => x.LocationId!.Value).Distinct().ToList();
        var locations = await _context.Locations.AsNoTracking()
            .Where(x => locationIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x);

        var lotIds = items.Where(x => x.PaddyLotId.HasValue).Select(x => x.PaddyLotId!.Value).Distinct().ToList();
        var lotVariants = await _context.PaddyLots.AsNoTracking()
            .Where(x => lotIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ProductVariantId);

        foreach (var item in items)
        {
            var sourceIsQuarantine = item.LocationId.HasValue
                                     && locations.TryGetValue(item.LocationId.Value, out var loc)
                                     && loc.IsQuarantine;

            foreach (var row in item.Bags.Where(x => !x.IsDeleted))
            {
                if (!StockTakeBagDispositions.IsValid(row.Disposition))
                    return $"Bao #{row.BagNo}: cách xử lý '{row.Disposition}' không hợp lệ.";

                if (row.Disposition == StockTakeBagDispositions.Keep) continue;

                if (!row.Counted)
                    return $"Bao #{row.BagNo}: chưa tìm thấy bao thì không thể chọn cách xử lý '{row.Disposition}'.";

                if (row.Disposition == StockTakeBagDispositions.Quarantine && sourceIsQuarantine)
                    return $"Bao #{row.BagNo} đang ở ô cách ly — không thể chuyển cách ly thêm lần nữa.";

                if (row.Disposition == StockTakeBagDispositions.Release && !sourceIsQuarantine)
                    return $"Bao #{row.BagNo} không nằm ở ô cách ly nên không thể rút ra để cất về khu thường.";

                if (row.Disposition == StockTakeBagDispositions.Dispose)
                {
                    if (string.IsNullOrWhiteSpace(row.DispositionNote))
                        return $"Bao #{row.BagNo}: bỏ nguyên bao hỏng phải ghi rõ lý do.";
                    row.TargetLocationId = null;
                    continue;
                }

                var wantQuarantine = row.Disposition == StockTakeBagDispositions.Quarantine;
                var variantId = item.PaddyLotId.HasValue && lotVariants.TryGetValue(item.PaddyLotId.Value, out var v)
                    ? v
                    : item.ProductVariantId;

                if (!row.TargetLocationId.HasValue)
                {
                    row.TargetLocationId = await ResolveDefaultTargetLocationAsync(
                        stockTake.WarehouseId, variantId, item.LocationId,
                        row.CountedWeightKg ?? row.SystemWeightKg, wantQuarantine);
                    row.LastModifiedDate = now;
                    row.UpdatedBy = userId;
                }

                if (!row.TargetLocationId.HasValue)
                    return wantQuarantine
                        ? $"Bao #{row.BagNo}: kho chưa có ô cách ly nào còn chỗ. Vui lòng giải phóng hoặc thêm ô cách ly trước."
                        : $"Bao #{row.BagNo}: không còn cột thường nào đủ chỗ để cất lại. Vui lòng chọn vị trí khác.";

                var target = await _context.Locations.AsNoTracking()
                    .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == row.TargetLocationId.Value);
                if (target == null)
                    return $"Bao #{row.BagNo}: vị trí đích không tồn tại.";
                if (target.WarehouseId != stockTake.WarehouseId)
                    return $"Bao #{row.BagNo}: vị trí đích không thuộc kho của phiếu kiểm kê.";
                if (target.IsOutboundStaging)
                    return $"Bao #{row.BagNo}: không được cất vào khu chờ xuất.";
                if (wantQuarantine && !target.IsQuarantine)
                    return $"Bao #{row.BagNo}: vị trí đích phải là ô CÁCH LY.";
                if (!wantQuarantine && target.IsQuarantine)
                    return $"Bao #{row.BagNo}: vị trí đích phải là cột THƯỜNG.";
            }
        }

        return null;
    }
}

/// <summary>
/// Phạm vi chụp phiếu kiểm kê. Kho chỉ dán QR theo CỘT và thủ kho dỡ hàng theo cột
/// nên nghiệp vụ rút gọn còn đúng một giá trị; các phạm vi cũ (khu / lô / toàn kho)
/// chỉ còn tồn tại trong phiếu đã tạo trước đây.
/// </summary>
public static class ScopeTypes
{
    public const string Column = "COLUMN";
}

/// <summary>Kết quả đánh giá chất lượng của một bao khi kiểm kê.</summary>
public static class StockTakeQualityResults
{
    public const string Pass = "PASS";
    public const string IssueDetected = "ISSUE_DETECTED";

    public static bool IsValid(string? value) =>
        string.IsNullOrWhiteSpace(value) || value == Pass || value == IssueDetected;
}
