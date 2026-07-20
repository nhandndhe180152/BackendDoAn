using System.Collections.Generic;
using System.Linq;

namespace Backend.Share.Helpers;

public static class PaddyScheduleStatusHelper
{
    /// <summary>
    /// Tính toán mã trạng thái lịch thu mua lúa dựa trên độ hoàn tất cất kho thực tế của các phiếu.
    /// </summary>
    /// <param name="receiptWeights">Danh sách các cặp (khối lượng đã cất kho, khối lượng thực tế của phiếu)</param>
    /// <returns>Mã trạng thái: "WEIGHED", "PARTIALLY_STOCKED", hoặc "STOCKED"</returns>
    public static string DetermineScheduleStatus(IEnumerable<(decimal StoredWeightKg, decimal ActualWeightKg)> receiptWeights)
    {
        var weights = receiptWeights.ToList();
        if (weights.Count == 0)
        {
            return "CONFIRMED";
        }

        var allReceiptsStoredFully = true;
        var hasAnyStoreIn = false;

        foreach (var (stored, actual) in weights)
        {
            if (stored > 0)
            {
                hasAnyStoreIn = true;
            }

            if (stored < actual)
            {
                allReceiptsStoredFully = false;
            }
        }

        if (!hasAnyStoreIn)
        {
            return "WEIGHED";
        }

        if (allReceiptsStoredFully)
        {
            return "STOCKED";
        }

        return "PARTIALLY_STOCKED";
    }
}
