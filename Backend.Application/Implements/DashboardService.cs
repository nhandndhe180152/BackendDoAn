using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.Dashboard;
using Backend.Application.Interfaces;
using Backend.Domain.Abstractions.Repositories;
using Backend.Domain.Entities;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using System.Threading;
using Backend.Application.BackgroundJobs.DebtDueOverdue;
using Microsoft.EntityFrameworkCore;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Backend.Application.Implements;

public class DashboardService : IDashboardService
{
    private readonly IRepositoryBase<Inventory, int> _inventoryRepository;
    private readonly IRepositoryBase<PaddyLot, int> _paddyLotRepository;
    private readonly IRepositoryBase<MillingOrder, int> _millingOrderRepository;
    private readonly IRepositoryBase<SalesOrder, int> _salesOrderRepository;
    private readonly IRepositoryBase<SalesOrderStatus, int> _salesOrderStatusRepository;
    private readonly IRepositoryBase<PartyDebt, int> _partyDebtRepository;
    private readonly IRepositoryBase<DebtTransaction, int> _debtTransactionRepository;
    private readonly IRepositoryBase<Alert, int> _alertRepository;
    private readonly IRepositoryBase<Customer, int> _customerRepository;
    private readonly IRepositoryBase<Farmer, int> _farmerRepository;
    private readonly IInventoryStateAggregationService _aggregationService;
    private readonly IDebtAgingCalculationService _agingService;
    private readonly IApplicationDbContext _context;

    public DashboardService(
        IRepositoryBase<Inventory, int> inventoryRepository,
        IRepositoryBase<PaddyLot, int> paddyLotRepository,
        IRepositoryBase<MillingOrder, int> millingOrderRepository,
        IRepositoryBase<SalesOrder, int> salesOrderRepository,
        IRepositoryBase<SalesOrderStatus, int> salesOrderStatusRepository,
        IRepositoryBase<PartyDebt, int> partyDebtRepository,
        IRepositoryBase<DebtTransaction, int> debtTransactionRepository,
        IRepositoryBase<Alert, int> alertRepository,
        IRepositoryBase<Customer, int> customerRepository,
        IRepositoryBase<Farmer, int> farmerRepository,
        IInventoryStateAggregationService aggregationService,
        IDebtAgingCalculationService agingService,
        IApplicationDbContext context)
    {
        _inventoryRepository = inventoryRepository;
        _paddyLotRepository = paddyLotRepository;
        _millingOrderRepository = millingOrderRepository;
        _salesOrderRepository = salesOrderRepository;
        _salesOrderStatusRepository = salesOrderStatusRepository;
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _alertRepository = alertRepository;
        _customerRepository = customerRepository;
        _farmerRepository = farmerRepository;
        _aggregationService = aggregationService;
        _agingService = agingService;
        _context = context;
    }

    // H2: resolve Completed status ID by Name to avoid magic number
    private int? _completedSalesStatusId;
    private async Task<int> GetCompletedSalesOrderStatusIdAsync()
    {
        if (_completedSalesStatusId.HasValue) return _completedSalesStatusId.Value;
        var status = await _salesOrderStatusRepository.FirstOrDefaultAsync(
            x => x.Code == SalesOrderStatusNames.Completed && !x.IsDeleted);
        _completedSalesStatusId = status?.Id
            ?? throw new InvalidOperationException($"SalesOrderStatus with code '{SalesOrderStatusNames.Completed}' not found.");
        return _completedSalesStatusId.Value;
    }

    public async Task<ApiResponse> GetReportStatisticsAsync(string period)
    {
        var (start, end) = DateTimeExtensions.GetDateRange(period);
        var query = new DashboardQuery
        {
            FromDate = start,
            ToDate = end
        };
        return await GetSummaryAsync(query);
    }

    // ── Summary ─────────────────────────────────────────────────────────

    public async Task<ApiResponse> GetSummaryAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var fromDate = query.FromDate;
        var toDate   = query.ToDate;

        // 1. Inventory Summary using the shared query service
        var aggregates = await _aggregationService.GetAggregatesAsync(query.WarehouseId, null, CancellationToken.None);

        if (query.RiceVarietyId.HasValue)
        {
            aggregates = aggregates.Where(x => x.RiceVarietyId == query.RiceVarietyId.Value).ToList();
        }

        var onHandKg = aggregates.Sum(x => x.TotalOnHandKg);
        var sellableOnHandKg = aggregates.Sum(x => x.SellableOnHandKg);
        var quarantinedKg = aggregates.Sum(x => x.QuarantinedKg);
        var otherBlockedKg = aggregates.Sum(x => x.OtherBlockedKg);
        var reservedKg = aggregates.Sum(x => x.ReservedKg);
        var reservedSellableKg = aggregates.Sum(x => x.ReservedSellableKg);
        var availableKg = Math.Max(0m, sellableOnHandKg - reservedSellableKg);

        var inventorySummary = new InventorySummaryDto
        {
            OnHandKg = onHandKg,
            SellableOnHandKg = sellableOnHandKg,
            ReservedKg = reservedKg,
            QuarantinedKg = quarantinedKg,
            OtherBlockedKg = otherBlockedKg,
            AvailableKg = availableKg,
            PaddyKg = aggregates.Where(x => x.LotType == LotTypeConstants.Paddy).Sum(x => x.TotalOnHandKg),
            RiceKg = aggregates.Where(x => x.LotType == LotTypeConstants.Rice).Sum(x => x.TotalOnHandKg),
            ByproductKg = aggregates.Where(x => x.LotType == LotTypeConstants.ByProduct || (x.LotType == null && x.IsVariantByproduct)).Sum(x => x.TotalOnHandKg)
        };

        // 2. Debt Summary
        var debts = await _partyDebtRepository
            .FindByCondition(x => !x.IsDeleted && x.IsActive, false)
            .ToListAsync();

        var farmerPayable      = debts.Where(x => x.PartyType == LookupCodes.PartyType.Farmer && x.Direction == LookupCodes.DebtDirection.Payable).Sum(x => x.CurrentBalance);
        var customerReceivable = debts.Where(x => x.PartyType == LookupCodes.PartyType.Customer && x.Direction == LookupCodes.DebtDirection.Receivable).Sum(x => x.CurrentBalance);

        decimal overduePayable = 0;
        decimal overdueReceivable = 0;
        var nowLimit = DateTimeHelper.VietnamNow();

        // M4b: Bulk-load all CHARGE transactions for debts that have balance > 0 to prevent N+1 query
        var debtIdsWithBalance = debts.Where(x => x.CurrentBalance > 0).Select(x => x.Id).ToList();
        var agingResults = await _agingService.CalculatePartyDebtsAgingBatchAsync(debtIdsWithBalance, nowLimit, CancellationToken.None);

        foreach (var debt in debts.Where(x => x.CurrentBalance > 0))
        {
            decimal overdue = 0;
            if (agingResults.TryGetValue(debt.Id, out var aging))
            {
                overdue = aging.OverdueAmount;
            }

            if (debt.Direction == LookupCodes.DebtDirection.Payable) overduePayable += overdue;
            else overdueReceivable += overdue;
        }

        var debtSummary = new DebtSummaryDto
        {
            FarmerPayable      = farmerPayable,
            CustomerReceivable = customerReceivable,
            OverduePayable      = overduePayable,
            OverdueReceivable   = overdueReceivable
        };

        // 3. Milling Summary
        var millingQuery = _millingOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.CompletedAt.HasValue &&
                                  x.CompletedAt.Value >= fromDate && x.CompletedAt.Value < toDate, false);

        if (query.WarehouseId.HasValue)
        {
            millingQuery = millingQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            millingQuery = millingQuery.Where(x => x.MillingOrderOutputs.Any(o => o.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value));
        }

        var millingData = await millingQuery
            .Select(x => new { x.ComputedPaddyKg, x.TotalRiceOutputKg, LossKg = x.LossKg ?? 0 })
            .ToListAsync();

        var totalPaddy = millingData.Sum(x => x.ComputedPaddyKg);
        var totalRice  = millingData.Sum(x => x.TotalRiceOutputKg);

        var millingSummary = new MillingSummaryDto
        {
            PaddyConsumedKg = totalPaddy,
            RiceOutputKg    = totalRice,
            ActualYieldRate = totalPaddy > 0 ? totalRice / totalPaddy : 0,
            LossKg          = millingData.Sum(x => x.LossKg)
        };

        // 4. Sales Summary — H2: resolve status by Name, not hard-coded ID
        var completedStatusId = await GetCompletedSalesOrderStatusIdAsync();
        var salesQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.StatusId == completedStatusId &&
                                  x.OrderDate >= fromDate && x.OrderDate < toDate, false);

        if (query.WarehouseId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.SalesOrderItems.Any(i => i.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value));
        }

        var salesOrders = await salesQuery
            .Select(x => new { x.Id, x.TotalAmount, Deposit = x.DepositAmount ?? 0 })
            .ToListAsync();

        var completedSoIds = salesOrders.Select(x => x.Id).ToList();
        var payments = await _debtTransactionRepository
            .FindByCondition(x => !x.IsDeleted && x.TransactionType == LookupCodes.DebtTransactionType.Payment &&
                                  x.RefType == InventoryReferenceTypeConstants.SalesOrder && x.RefId.HasValue &&
                                  completedSoIds.Contains(x.RefId.Value), false)
            .SumAsync(x => x.Amount);

        var grossRev   = salesOrders.Sum(x => x.TotalAmount);
        var deposits   = salesOrders.Sum(x => x.Deposit);
        var collected  = deposits + payments;

        var salesSummary = new SalesSummaryDto
        {
            GrossRevenue        = grossRev,
            AmountCollected     = collected,
            OutstandingAmount   = Math.Max(0, grossRev - collected),
            CompletedOrderCount = salesOrders.Count
        };

        // 5. Alerts Summary
        var alertsQuery = _alertRepository.FindByCondition(x => !x.IsDeleted && x.Status == AlertConstants.Status.Open, false);

        if (query.WarehouseId.HasValue)
        {
            alertsQuery = alertsQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var openAlerts = await alertsQuery
            .Select(x => new { x.Severity })
            .ToListAsync();

        var quarantinedLotCount = await _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted && !x.Status.IsSellable && x.RemainingWeightKg > 0, false)
            .CountAsync();

        var thirtyDaysAgo = nowLimit.AddDays(-30);
        var overdueLotCount = await _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted && x.RemainingWeightKg > 0 && x.Status.IsSellable &&
                                 (!x.QualityInspections.Any(q => !q.IsDeleted) || 
                                  x.QualityInspections.Where(q => !q.IsDeleted).Max(q => q.InspectedAt) < thirtyDaysAgo), false)
            .CountAsync();

        // 6. Recent Alerts (Top 5)
        var recentAlerts = await _alertRepository.FindByCondition(x => !x.IsDeleted && x.Status == AlertConstants.Status.Open, false)
            .OrderByDescending(x => x.CreatedDate)
            .Take(5)
            .Select(x => new AlertItemDto
            {
                Id = x.Id,
                Message = x.Message,
                Severity = x.Severity,
                CreatedAt = x.CreatedDate
            })
            .ToListAsync();

        foreach (var alert in recentAlerts)
        {
            alert.TimeAgo = GetTimeAgo(alert.CreatedAt);
        }

        // 7. Efficiency Metrics
        var efficiency = await GetOperationalEfficiencyInternalAsync(query);

        // 8. Delta Today
        var today = DateTimeHelper.VietnamNow().Date;
        var txQuery = _context.InventoryTransactions.Where(x => !x.IsDeleted && x.CreatedDate >= today);
        if (query.WarehouseId.HasValue)
        {
            txQuery = txQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        var todayTransactions = await txQuery
            .Select(x => new {
                x.Quantity,
                LotType = x.PaddyLot != null ? x.PaddyLot.LotType : null
            })
            .ToListAsync();

        var paddyDelta = todayTransactions.Where(x => x.LotType == LotTypeConstants.Paddy).Sum(x => x.Quantity);
        var riceDelta = todayTransactions.Where(x => x.LotType == LotTypeConstants.Rice).Sum(x => x.Quantity);

        inventorySummary.PaddyDeltaTodayKg = paddyDelta;
        inventorySummary.RiceDeltaTodayKg = riceDelta;

        // 9. Pending Delivery Tasks
        var pendingDeliveryStatuses = new[] { 
            SalesOrderStatusNames.PendingConfirm, 
            SalesOrderStatusNames.Reserved, 
            SalesOrderStatusNames.AwaitingMilling, 
            SalesOrderStatusNames.Preparing, 
            SalesOrderStatusNames.Delivering 
        };
        var actionRequiredStatuses = new[] { 
            SalesOrderStatusNames.New, 
            SalesOrderStatusNames.PendingConfirm 
        };

        var pendingDeliveryOrders = await _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted, false)
            .Where(x => pendingDeliveryStatuses.Contains(x.Status.Code) || x.Status.Code == SalesOrderStatusNames.New)
            .Select(x => new { Name = x.Status.Code })
            .ToListAsync();

        salesSummary.PendingDeliveryCount = pendingDeliveryOrders.Count;
        salesSummary.PendingDeliveryActionRequiredCount = pendingDeliveryOrders
            .Count(x => actionRequiredStatuses.Contains(x.Name));

        var alertsSummary = new AlertsSummaryDto
        {
            OpenCount                 = openAlerts.Count,
            CriticalCount             = openAlerts.Count(x => x.Severity == AlertConstants.Severity.Critical),
            QuarantinedLotCount       = quarantinedLotCount,
            InspectionOverdueLotCount = overdueLotCount
        };

        return ApiResponse.Success(new DashboardSummaryDto
        {
            Inventory = inventorySummary,
            Debt      = debtSummary,
            Milling   = millingSummary,
            Sales     = salesSummary,
            Alerts    = alertsSummary,
            RecentAlerts = recentAlerts,
            Efficiency = efficiency
        });
    }

    // ── Drill-down reports ──────────────────────────────────────────────

    public async Task<ApiResponse> GetInventoryByLotReportAsync(DashboardQuery query)
    {
        var baseQuery = _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted && x.RemainingWeightKg > 0, false);

        if (query.WarehouseId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.RiceVarietyId == query.RiceVarietyId.Value);
        }
        if (query.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ProductVariantId == query.ProductVariantId.Value);
        }
        if (query.PaddyLotId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Id == query.PaddyLotId.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.ProductType))
        {
            var productType = query.ProductType.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(x => x.LotType == productType);
        }

        var lots = await baseQuery
            .Select(x => new
            {
                x.Id,
                x.LotCode,
                x.LotType,
                RiceVarietyName = x.RiceVariety != null ? x.RiceVariety.Name : null,
                SKU = x.ProductVariant.SKU,
                ProductVariantName = x.ProductVariant.Name,
                WarehouseName = x.Warehouse.Name,
                SourceBagCount = x.SourceReceipt != null
                    ? x.SourceReceipt.BagCount
                    : x.SourceMillingOrder != null
                        ? x.SourceMillingOrder.MillingOrderOutputs
                            .Where(o => !o.IsDeleted && o.OutputLotId == x.Id)
                            .Select(o => o.BagCount)
                            .FirstOrDefault()
                        : null,
                x.InboundDate,
                x.InitialWeightKg,
                x.RemainingWeightKg,
                StatusName = x.Status.Name,
                StatusCode = x.Status.Code,
                IsSellable = x.Status.IsSellable,
                LastInspectionAt = x.QualityInspections
                    .Where(q => !q.IsDeleted)
                    .OrderByDescending(q => q.InspectedAt)
                    .Select(q => (DateTime?)q.InspectedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var lotIds = lots.Select(l => l.Id).ToList();

        // Get all aggregates for these lots
        var aggregates = await _aggregationService.GetAggregatesAsync(query.WarehouseId, null, CancellationToken.None);
        var lotAggregates = aggregates.Where(x => x.PaddyLotId.HasValue && lotIds.Contains(x.PaddyLotId.Value)).ToList();
        if (query.LocationId.HasValue)
        {
            lotAggregates = lotAggregates
                .Where(x => x.LocationId == query.LocationId.Value)
                .ToList();
        }

        var aggMap = lotAggregates.GroupBy(x => x.PaddyLotId!.Value).ToDictionary(g => g.Key, g => g.ToList());

        var report = lots.Select(lot =>
        {
            decimal onHandKg = 0m;
            decimal reservedKg = 0m;
            decimal quarantinedKg = 0m;
            decimal availableKg = 0m;
            var locationCodesList = new List<string>();

            bool lotStatusQuarantine = lot.StatusCode == LotStatusCodeConstants.Quarantine;
            bool hasQuarantineLocation = false;

            if (aggMap.TryGetValue(lot.Id, out var lotLines))
            {
                foreach (var line in lotLines)
                {
                    onHandKg += line.TotalOnHandKg;
                    reservedKg += line.ReservedKg;
                    quarantinedKg += line.QuarantinedKg;
                    availableKg += line.AvailableKg;

                    if (!string.IsNullOrEmpty(line.LocationCode))
                    {
                        locationCodesList.Add(line.LocationCode);
                    }

                    // Check if location itself was quarantined (QuarantinedKg > 0 on a non-QUARANTINE lot status)
                    if (line.QuarantinedKg > 0 && !lotStatusQuarantine)
                    {
                        hasQuarantineLocation = true;
                    }
                }
            }
            else
            {
                onHandKg = lot.RemainingWeightKg;
                if (lotStatusQuarantine)
                {
                    quarantinedKg = lot.RemainingWeightKg;
                    availableKg = 0m;
                }
                else
                {
                    availableKg = lot.RemainingWeightKg;
                }
            }

            var isQuarantined = lotStatusQuarantine || quarantinedKg > 0;
            
            string quarantineSource = "NONE";
            if (lotStatusQuarantine && hasQuarantineLocation)
            {
                quarantineSource = "BOTH";
            }
            else if (lotStatusQuarantine)
            {
                quarantineSource = "LOT_STATUS";
            }
            else if (quarantinedKg > 0)
            {
                quarantineSource = "LOCATION";
            }

            var locationsStr = locationCodesList.Count > 0 
                ? string.Join(", ", locationCodesList.Distinct()) 
                : null;

            return new InventoryByLotReportDto
            {
                LotId = lot.Id,
                LotCode = lot.LotCode,
                LotType = lot.LotType,
                RiceVariety = lot.RiceVarietyName,
                SKU = lot.SKU,
                ProductVariant = lot.ProductVariantName,
                Warehouse = lot.WarehouseName,
                Location = locationsStr,
                Locations = locationsStr,
                // Số bao chỉ đáng tin ở thời điểm tạo lô; sau xuất một phần hệ thống
                // quản lý tồn chính xác theo kg nên FE hiển thị đây là số tham chiếu.
                BagCount = lot.SourceBagCount,
                InboundDate = lot.InboundDate,
                InitialWeightKg = lot.InitialWeightKg,
                RemainingWeightKg = lot.RemainingWeightKg,
                OnHandKg = onHandKg,
                ReservedKg = reservedKg,
                QuarantinedKg = quarantinedKg,
                AvailableKg = availableKg,
                IsQuarantined = isQuarantined,
                QuarantineSource = quarantineSource,
                LastInspectionAt = lot.LastInspectionAt,
                LotStatus = lot.StatusName
            };
        })
        .Where(x => !query.LocationId.HasValue || !string.IsNullOrWhiteSpace(x.Location))
        .OrderBy(x => x.LotCode)
        .ToList();

        var chart = report
            .GroupBy(x => x.LotType)
            .Select(g => new ReportChartPointDto
            {
                Label = g.Key,
                Value = g.Sum(x => x.AvailableKg),
                SecondaryValue = g.Sum(x => x.QuarantinedKg)
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(report, query, chart));
    }

    public async Task<ApiResponse> GetInventoryByWarehouseReportAsync(DashboardQuery query)
    {
        var aggregates = await _aggregationService.GetAggregatesAsync(query.WarehouseId, null, CancellationToken.None);

        var quarantineLocations = await _inventoryRepository.FindByCondition(x => !x.IsDeleted && x.Location != null && x.Location.IsQuarantine, false)
            .Select(x => new { x.WarehouseId, LocationId = x.LocationId!.Value })
            .Distinct()
            .ToListAsync();

        var qLocMap = quarantineLocations.GroupBy(x => x.WarehouseId).ToDictionary(g => g.Key, g => g.Count());

        var grouped = aggregates
            .GroupBy(x => new { x.WarehouseId, x.WarehouseName })
            .Select(g => {
                var onHandKg = g.Sum(x => x.TotalOnHandKg);
                var sellableOnHandKg = g.Sum(x => x.SellableOnHandKg);
                var reservedKg = g.Sum(x => x.ReservedKg);
                var quarantinedKg = g.Sum(x => x.QuarantinedKg);
                var reservedSellableKg = g.Sum(x => x.ReservedSellableKg);
                var availableKg = Math.Max(0m, sellableOnHandKg - reservedSellableKg);

                var quarantinedLotCount = g.Where(x => x.QuarantinedKg > 0 && x.PaddyLotId.HasValue)
                    .Select(x => x.PaddyLotId!.Value)
                    .Distinct()
                    .Count();

                var quarantineLocCount = qLocMap.GetValueOrDefault(g.Key.WarehouseId, 0);

                return new InventoryByWarehouseReportDto
                {
                    WarehouseId = g.Key.WarehouseId,
                    WarehouseName = g.Key.WarehouseName,
                    OnHandKg = onHandKg,
                    SellableOnHandKg = sellableOnHandKg,
                    ReservedKg = reservedKg,
                    QuarantinedKg = quarantinedKg,
                    AvailableKg = availableKg,
                    PaddyKg = g.Where(x => x.LotType == LotTypeConstants.Paddy).Sum(x => x.TotalOnHandKg),
                    RiceKg = g.Where(x => x.LotType == LotTypeConstants.Rice).Sum(x => x.TotalOnHandKg),
                    ByproductKg = g.Where(x => x.LotType == LotTypeConstants.ByProduct || (x.LotType == null && x.IsVariantByproduct)).Sum(x => x.TotalOnHandKg),
                    QuarantinedLotCount = quarantinedLotCount,
                    QuarantineLocationCount = quarantineLocCount
                };
            })
            .ToList();

        return ApiResponse.Success(grouped);
    }

    public async Task<ApiResponse> GetInventoryByProductVariantReportAsync(DashboardQuery query)
    {
        var aggregates = await _aggregationService.GetAggregatesAsync(query.WarehouseId, null, CancellationToken.None);

        var grouped = aggregates
            .GroupBy(x => new { x.WarehouseId, x.WarehouseName, x.ProductVariantId, x.SKU, x.ProductVariantName, x.ProductName })
            .Select(g =>
            {
                var onHandKg = g.Sum(x => x.TotalOnHandKg);
                var sellableOnHandKg = g.Sum(x => x.SellableOnHandKg);
                var reservedKg = g.Sum(x => x.ReservedKg);
                var quarantinedKg = g.Sum(x => x.QuarantinedKg);
                var reservedSellableKg = g.Sum(x => x.ReservedSellableKg);
                var availableKg = Math.Max(0m, sellableOnHandKg - reservedSellableKg);

                var quarantinedLotCount = g.Where(x => x.QuarantinedKg > 0 && x.PaddyLotId.HasValue)
                    .Select(x => x.PaddyLotId!.Value)
                    .Distinct()
                    .Count();

                var quarantineRatio = onHandKg > 0m ? quarantinedKg / onHandKg : 0m;

                return new InventoryByProductVariantReportDto
                {
                    WarehouseId = g.Key.WarehouseId,
                    WarehouseName = g.Key.WarehouseName,
                    ProductVariantId = g.Key.ProductVariantId,
                    SKU = g.Key.SKU,
                    ProductVariantName = g.Key.ProductVariantName,
                    ProductName = g.Key.ProductName,
                    OnHandKg = onHandKg,
                    SellableOnHandKg = sellableOnHandKg,
                    ReservedKg = reservedKg,
                    QuarantinedKg = quarantinedKg,
                    AvailableKg = availableKg,
                    QuarantinedLotCount = quarantinedLotCount,
                    QuarantineRatio = quarantineRatio
                };
            })
            .ToList();

        return ApiResponse.Success(grouped);
    }

    public async Task<ApiResponse> GetTwoWayDebtReportAsync(DashboardQuery query)
    {
        var debts = await _partyDebtRepository
            .FindByCondition(x => !x.IsDeleted && x.IsActive, false)
            .ToListAsync();

        if (debts.Count == 0)
            return ApiResponse.Success(new List<TwoWayDebtReportDto>());

        var nowLimit = DateTimeHelper.VietnamNow();

        // M4: Bulk-load customers and farmers — 2 queries instead of N queries
        var customerIds = debts.Where(d => d.PartyType == LookupCodes.PartyType.Customer).Select(d => d.PartyId).Distinct().ToList();
        var farmerIds   = debts.Where(d => d.PartyType == LookupCodes.PartyType.Farmer).Select(d => d.PartyId).Distinct().ToList();

        var customerNames = await _customerRepository
            .FindByCondition(x => customerIds.Contains(x.Id) && !x.IsDeleted, false)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var farmerNames = await _farmerRepository
            .FindByCondition(x => farmerIds.Contains(x.Id) && !x.IsDeleted, false)
            .Select(x => new { x.Id, x.Name })
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        // M4: Bulk-load all CHARGE transactions for debts that have balance > 0
        var debtIdsWithBalance = debts.Where(d => d.CurrentBalance > 0).Select(d => d.Id).ToList();
        var agingResults = await _agingService.CalculatePartyDebtsAgingBatchAsync(debtIdsWithBalance, nowLimit, CancellationToken.None);

        var report = new List<TwoWayDebtReportDto>(debts.Count);

        foreach (var debt in debts)
        {
            var name = debt.PartyType switch
            {
                LookupCodes.PartyType.Customer => customerNames.GetValueOrDefault(debt.PartyId) ?? $"Khách hàng {debt.PartyId}",
                LookupCodes.PartyType.Farmer   => farmerNames.GetValueOrDefault(debt.PartyId) ?? $"Nông dân {debt.PartyId}",
                _          => $"Bên {debt.PartyId}"
            };

            decimal overdue = 0;
            if (debt.CurrentBalance > 0 && agingResults.TryGetValue(debt.Id, out var aging))
            {
                overdue = aging.OverdueAmount;
            }

            report.Add(new TwoWayDebtReportDto
            {
                Direction      = debt.Direction,
                PartyType      = debt.PartyType,
                PartyId        = debt.PartyId,
                PartyName      = name,
                CurrentBalance = debt.CurrentBalance,
                OverdueAmount  = overdue
            });
        }

        return ApiResponse.Success(report);
    }

    public async Task<ApiResponse> GetDebtDocumentsReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var debtQuery = _context.PartyDebts
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.IsActive);

        if (query.FarmerId.HasValue)
        {
            debtQuery = debtQuery.Where(x =>
                x.PartyType == LookupCodes.PartyType.Farmer &&
                x.PartyId == query.FarmerId.Value);
        }
        if (query.CustomerId.HasValue)
        {
            debtQuery = debtQuery.Where(x =>
                x.PartyType == LookupCodes.PartyType.Customer &&
                x.PartyId == query.CustomerId.Value);
        }

        var debts = await debtQuery.ToListAsync();
        var debtIds = debts.Select(x => x.Id).ToList();
        var transactions = await _context.DebtTransactions
            .AsNoTracking()
            .Where(x => debtIds.Contains(x.PartyDebtId) && !x.IsDeleted)
            .ToListAsync();
        var txMap = transactions
            .GroupBy(x => x.PartyDebtId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var farmerIds = debts
            .Where(x => x.PartyType == LookupCodes.PartyType.Farmer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();
        var customerIds = debts
            .Where(x => x.PartyType == LookupCodes.PartyType.Customer)
            .Select(x => x.PartyId)
            .Distinct()
            .ToList();

        var farmerNames = await _context.Farmers
            .AsNoTracking()
            .Where(x => farmerIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, x => x.Name);
        var customerNames = await _context.Customers
            .AsNoTracking()
            .Where(x => customerIds.Contains(x.Id) && !x.IsDeleted)
            .ToDictionaryAsync(x => x.Id, x => x.Name);

        var allocations = new List<(PartyDebt Debt, DebtDocumentAllocation Document)>();
        foreach (var debt in debts)
        {
            txMap.TryGetValue(debt.Id, out var debtTransactions);
            allocations.AddRange(_agingService
                .CalculateDebtDocuments(debt, debtTransactions ?? new List<DebtTransaction>())
                .Select(x => (debt, x)));
        }

        allocations = allocations
            .Where(x => x.Document.TransactionDate >= query.FromDate &&
                        x.Document.TransactionDate < query.ToDate)
            .ToList();

        var receiptIds = allocations
            .Where(x => string.Equals(x.Document.RefType, "PADDY_RECEIPT", StringComparison.OrdinalIgnoreCase) &&
                        x.Document.RefId.HasValue)
            .Select(x => x.Document.RefId!.Value)
            .Distinct()
            .ToList();
        var salesOrderIds = allocations
            .Where(x => (string.Equals(x.Document.RefType, "SALES_ORDER", StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(x.Document.RefType, "SALES_ORDER_DEPOSIT", StringComparison.OrdinalIgnoreCase)) &&
                        x.Document.RefId.HasValue)
            .Select(x => x.Document.RefId!.Value)
            .Distinct()
            .ToList();

        var receiptCodes = await _context.PaddyPurchaseReceipts
            .AsNoTracking()
            .Where(x => receiptIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ReceiptCode);
        var salesCodes = await _context.SalesOrders
            .AsNoTracking()
            .Where(x => salesOrderIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.SOCode);

        var today = DateTimeHelper.VietnamNow().Date;
        var rows = allocations.Select(item =>
        {
            var debt = item.Debt;
            var document = item.Document;
            var dueDate = document.DueDate?.Date;
            var status = document.OutstandingAmount <= 0m
                ? "PAID"
                : dueDate.HasValue && dueDate.Value < today
                    ? "OVERDUE"
                    : document.PaidAmount > 0m ? "PARTIAL" : "UNPAID";
            var partyName = debt.PartyType == LookupCodes.PartyType.Farmer
                ? farmerNames.GetValueOrDefault(debt.PartyId) ?? $"Nông dân {debt.PartyId}"
                : customerNames.GetValueOrDefault(debt.PartyId) ?? $"Khách hàng {debt.PartyId}";

            var code = document.RefId.HasValue &&
                       string.Equals(document.RefType, "PADDY_RECEIPT", StringComparison.OrdinalIgnoreCase)
                ? receiptCodes.GetValueOrDefault(document.RefId.Value)
                : document.RefId.HasValue &&
                  (string.Equals(document.RefType, "SALES_ORDER", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(document.RefType, "SALES_ORDER_DEPOSIT", StringComparison.OrdinalIgnoreCase))
                    ? salesCodes.GetValueOrDefault(document.RefId.Value)
                    : null;

            return new DebtDocumentReportDto
            {
                Direction = debt.Direction,
                PartyType = debt.PartyType,
                PartyId = debt.PartyId,
                PartyName = partyName,
                RefType = document.RefType,
                RefId = document.RefId,
                DocumentCode = code ?? $"{document.RefType ?? "DEBT"}-{document.RefId?.ToString() ?? document.ChargeTransactionId.ToString()}",
                TransactionDate = document.TransactionDate,
                DueDate = document.DueDate,
                TotalAmount = document.TotalAmount,
                PaidAmount = document.PaidAmount,
                OutstandingAmount = document.OutstandingAmount,
                Status = status,
                DaysOverdue = dueDate.HasValue && dueDate.Value < today
                    ? (today - dueDate.Value).Days
                    : 0
            };
        })
        .Where(x => string.IsNullOrWhiteSpace(query.Status) ||
                    x.Status.Equals(query.Status, StringComparison.OrdinalIgnoreCase))
        .OrderByDescending(x => x.DueDate ?? x.TransactionDate)
        .ToList();

        var chart = rows
            .GroupBy(x => x.Status)
            .Select(g => new ReportChartPointDto
            {
                Label = g.Key,
                Value = g.Sum(x => x.OutstandingAmount)
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(rows, query, chart));
    }

    public async Task<ApiResponse> GetPurchaseReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var baseQuery = _context.PaddyPurchaseReceipts
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.ReceiptDate >= query.FromDate &&
                        x.ReceiptDate < query.ToDate);

        if (query.WarehouseId.HasValue)
            baseQuery = baseQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        if (query.RiceVarietyId.HasValue)
            baseQuery = baseQuery.Where(x => x.RiceVarietyId == query.RiceVarietyId.Value);
        if (query.FarmerId.HasValue)
            baseQuery = baseQuery.Where(x => x.FarmerId == query.FarmerId.Value);
        if (query.ProductVariantId.HasValue)
            baseQuery = baseQuery.Where(x =>
                x.PaddyLot != null &&
                x.PaddyLot.ProductVariantId == query.ProductVariantId.Value);
        if (query.PaddyLotId.HasValue)
            baseQuery = baseQuery.Where(x =>
                x.PaddyLot != null &&
                x.PaddyLot.Id == query.PaddyLotId.Value);
        if (query.LocationId.HasValue)
            baseQuery = baseQuery.Where(x =>
                x.PaddyLot != null &&
                x.PaddyLot.LocationId == query.LocationId.Value);
        if (!string.IsNullOrWhiteSpace(query.ProductType))
        {
            var productType = query.ProductType.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(x =>
                x.PaddyLot != null &&
                x.PaddyLot.LotType == productType);
        }

        var rows = await baseQuery
            .OrderByDescending(x => x.ReceiptDate)
            .Select(x => new PurchaseReportDto
            {
                ReceiptId = x.Id,
                ReceiptCode = x.ReceiptCode,
                ReceiptDate = x.ReceiptDate,
                FarmerId = x.FarmerId,
                FarmerName = x.Farmer.Name,
                RiceVarietyId = x.RiceVarietyId,
                RiceVarietyName = x.RiceVariety != null ? x.RiceVariety.Name : null,
                WarehouseName = x.Warehouse.Name,
                WeightKg = x.ActualWeightKg,
                BagCount = x.BagCount,
                UnitPrice = x.AgreedPrice,
                TotalAmount = x.TotalAmount,
                PaidAmount = x.PaidAmount,
                DebtAmount = x.DebtAmount,
                QualitySummary = x.QualityJson
            })
            .ToListAsync();

        var chart = rows
            .GroupBy(x => x.ReceiptDate.Date)
            .OrderBy(x => x.Key)
            .Select(g => new ReportChartPointDto
            {
                Label = g.Key.ToString("dd/MM"),
                Value = g.Sum(x => x.WeightKg),
                SecondaryValue = g.Sum(x => x.TotalAmount)
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(rows, query, chart));
    }

    public async Task<ApiResponse> GetMillingYieldReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var baseQuery = _millingOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.CompletedAt.HasValue &&
                                  x.CompletedAt.Value >= query.FromDate && x.CompletedAt.Value < query.ToDate, false);

        if (query.WarehouseId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.MillingOrderOutputs.Any(o => o.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value));
        }
        if (query.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.MillingOrderOutputs
                .Any(o => !o.IsDeleted && o.ProductVariantId == query.ProductVariantId.Value));
        }
        if (query.PaddyLotId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.MillingOrderInputs
                .Any(i => !i.IsDeleted && i.PaddyLotId == query.PaddyLotId.Value));
        }
        if (query.LocationId.HasValue)
        {
            baseQuery = baseQuery.Where(x =>
                x.MillingOrderInputs.Any(i => !i.IsDeleted && i.LocationId == query.LocationId.Value) ||
                x.MillingOrderOutputs.Any(o => !o.IsDeleted && o.LocationId == query.LocationId.Value));
        }

        var list = await baseQuery
            .OrderByDescending(x => x.CompletedAt)
            .Select(x => new MillingYieldReportDto
            {
                MillingOrderId    = x.Id,
                MillingCode       = x.MillingCode,
                WarehouseName     = x.Warehouse.Name,
                ComputedPaddyKg   = x.ComputedPaddyKg,
                TotalRiceOutputKg = x.TotalRiceOutputKg,
                ByproductKg       = x.ByproductKg ?? x.MillingOrderOutputs
                    .Where(o => !o.IsDeleted && o.IsByproduct)
                    .Sum(o => o.OutputWeightKg),
                ActualYieldRate   = x.ComputedPaddyKg > 0 ? x.TotalRiceOutputKg / x.ComputedPaddyKg : 0,
                LossKg            = x.LossKg ?? 0,
                TotalCost         = x.TotalCost ?? 0,
                CompletedAt       = x.CompletedAt
            })
            .ToListAsync();

        var chart = list.Select(x => new ReportChartPointDto
        {
            Label = x.MillingCode,
            Value = x.ActualYieldRate * 100m,
            SecondaryValue = x.LossKg
        }).ToList();

        return ApiResponse.Success(ToReportPage(list, query, chart));
    }

    public async Task<ApiResponse> GetSalesRevenueReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        // H2: resolve status by Name, not hard-coded ID
        var completedStatusId = await GetCompletedSalesOrderStatusIdAsync();
        var baseQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.StatusId == completedStatusId &&
                                  x.OrderDate >= query.FromDate && x.OrderDate < query.ToDate, false);

        if (query.WarehouseId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.SalesOrderItems.Any(i => i.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value));
        }
        if (query.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.SalesOrderItems.Any(i => i.ProductVariantId == query.ProductVariantId.Value));
        }
        if (query.CustomerId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.CustomerId == query.CustomerId.Value);
        }
        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            var channel = query.Channel.Trim().ToUpperInvariant();
            baseQuery = baseQuery.Where(x => x.Channel == channel);
        }

        var orders = await baseQuery
            .OrderByDescending(x => x.OrderDate)
            .Select(x => new
            {
                x.Id,
                x.OrderDate,
                x.SOCode,
                CustomerName = x.Customer.Name,
                x.Channel,
                x.TotalAmount,
                DepositAmount = x.DepositAmount ?? 0,
                StatusName = x.Status.Name,
                Items = x.SalesOrderItems.Where(i => !i.IsDeleted).Select(i => new
                {
                    i.QuantityOrdered,
                    i.ProductVariant.SKU,
                    VariantName = i.ProductVariant.Name,
                    UnitWeight = i.ProductVariant.Weight
                }).ToList()
            })
            .ToListAsync();

        var orderIds = orders.Select(x => x.Id).ToList();

        var paymentGroups = await _debtTransactionRepository
            .FindByCondition(x => !x.IsDeleted && x.TransactionType == LookupCodes.DebtTransactionType.Payment &&
                                  x.RefType == InventoryReferenceTypeConstants.SalesOrder && x.RefId.HasValue &&
                                  orderIds.Contains(x.RefId.Value), false)
            .GroupBy(x => x.RefId!.Value)
            .Select(g => new { OrderId = g.Key, TotalPaid = g.Sum(x => x.Amount) })
            .ToDictionaryAsync(x => x.OrderId, x => x.TotalPaid);

        var report = orders.Select(o =>
        {
            paymentGroups.TryGetValue(o.Id, out var paid);
            var collected = o.DepositAmount + paid;
            return new SalesRevenueReportDto
            {
                SalesOrderId     = o.Id,
                OrderDate         = o.OrderDate,
                SOCode            = o.SOCode,
                CustomerName      = o.CustomerName,
                Channel           = o.Channel,
                ItemSummary       = string.Join(", ", o.Items.Select(i => $"{i.SKU} - {i.VariantName}")),
                TotalQuantity     = o.Items.Sum(i => i.QuantityOrdered),
                TotalWeightKg     = o.Items.Sum(i => i.QuantityOrdered * (i.UnitWeight > 0 ? i.UnitWeight : 1)),
                TotalAmount       = o.TotalAmount,
                DepositAmount     = o.DepositAmount,
                AmountCollected   = collected,
                OutstandingAmount = Math.Max(0, o.TotalAmount - collected),
                StatusName        = o.StatusName
            };
        }).ToList();

        var chart = report
            .GroupBy(x => x.Channel)
            .Select(g => new ReportChartPointDto
            {
                Label = g.Key,
                Value = g.Sum(x => x.TotalAmount),
                SecondaryValue = g.Sum(x => x.AmountCollected)
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(report, query, chart));
    }

    public async Task<ApiResponse> GetQualityAlertsReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var nowLimit = DateTimeHelper.VietnamNow();
        var thirtyDaysAgo = nowLimit.AddDays(-30);

        var baseQuery = _paddyLotRepository
            .FindByCondition(x => !x.IsDeleted && x.RemainingWeightKg > 0, false);

        if (query.WarehouseId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.RiceVarietyId == query.RiceVarietyId.Value);
        }
        if (query.ProductVariantId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.ProductVariantId == query.ProductVariantId.Value);
        }
        if (query.PaddyLotId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.Id == query.PaddyLotId.Value);
        }
        if (query.LocationId.HasValue)
        {
            baseQuery = baseQuery.Where(x => x.LocationId == query.LocationId.Value);
        }

        var rawLots = await baseQuery
            .Select(x => new
            {
                x.Id,
                x.LotCode,
                x.LotType,
                ProductVariantName = x.ProductVariant.Name,
                WarehouseName = x.Warehouse.Name,
                LocationCode = x.Location != null
                    ? (x.Location.SlotCode ?? x.Location.ZoneName)
                    : null,
                x.RemainingWeightKg,
                x.QualityStatus,
                IsSellable = x.Status.IsSellable,
                LastInspection = x.QualityInspections
                    .Where(q => !q.IsDeleted)
                    .OrderByDescending(q => q.InspectedAt)
                    .Select(q => new
                    {
                        q.InspectedAt,
                        q.AffectedWeightKg,
                        q.MoisturePercent,
                        q.MoldLevel,
                        q.PestLevel,
                        q.PackagingStatus,
                        q.Handling,
                        q.PassedInspection
                    })
                    .FirstOrDefault()
            })
            .ToListAsync();

        var list = rawLots
            .Select(x =>
            {
                var isOverdue = x.LastInspection == null || x.LastInspection.InspectedAt < thirtyDaysAgo;
                var risks = new List<string>();
                if (x.LastInspection?.MoisturePercent is > 14m)
                    risks.Add($"Ẩm {x.LastInspection.MoisturePercent:0.#}%");
                if (!string.IsNullOrWhiteSpace(x.LastInspection?.MoldLevel) &&
                    !x.LastInspection.MoldLevel.Equals("Không", StringComparison.OrdinalIgnoreCase))
                    risks.Add($"Mốc: {x.LastInspection.MoldLevel}");
                if (!string.IsNullOrWhiteSpace(x.LastInspection?.PestLevel) &&
                    !x.LastInspection.PestLevel.Equals("Không", StringComparison.OrdinalIgnoreCase))
                    risks.Add($"Mọt: {x.LastInspection.PestLevel}");
                if (!string.IsNullOrWhiteSpace(x.LastInspection?.PackagingStatus) &&
                    !x.LastInspection.PackagingStatus.Equals("Nguyên", StringComparison.OrdinalIgnoreCase))
                    risks.Add($"Bao: {x.LastInspection.PackagingStatus}");
                if (isOverdue) risks.Add("Trễ kiểm định");

                var isQuarantined = !x.IsSellable;
                var severity = isQuarantined || x.LastInspection?.PassedInspection == false
                    ? "HIGH"
                    : isOverdue || risks.Count > 0 ? "MEDIUM" : "LOW";
                return new QualityAlertReportDto
                {
                    LotId              = x.Id,
                    LotCode            = x.LotCode,
                    LotType            = x.LotType,
                    ProductVariantName = x.ProductVariantName,
                    WarehouseName      = x.WarehouseName,
                    Location           = x.LocationCode,
                    RemainingWeightKg  = x.RemainingWeightKg,
                    AffectedWeightKg   = x.LastInspection?.AffectedWeightKg ?? x.RemainingWeightKg,
                    QualityStatus      = x.QualityStatus,
                    RiskSummary        = risks.Count > 0 ? string.Join(", ", risks) : "Không ghi nhận",
                    Severity           = severity,
                    Recommendation     = x.LastInspection?.Handling,
                    LastInspectionAt   = x.LastInspection?.InspectedAt,
                    IsQuarantined      = isQuarantined,
                    IsInspectionOverdue = isOverdue
                };
            })
            .Where(x => x.IsQuarantined || x.IsInspectionOverdue)
            .OrderByDescending(x => x.Severity)
            .ToList();

        var chart = list
            .GroupBy(x => x.Severity)
            .Select(g => new ReportChartPointDto
            {
                Label = g.Key,
                Value = g.Sum(x => x.AffectedWeightKg)
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(list, query, chart));
    }

    public async Task<ApiResponse> GetRelativeProfitReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var outboundQuery = _context.OutboundOrders
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.CompletedDate.HasValue &&
                        x.CompletedDate.Value >= query.FromDate &&
                        x.CompletedDate.Value < query.ToDate);

        if (query.WarehouseId.HasValue)
            outboundQuery = outboundQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        if (query.CustomerId.HasValue)
            outboundQuery = outboundQuery.Where(x => x.SalesOrder.CustomerId == query.CustomerId.Value);
        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            var channel = query.Channel.Trim().ToUpperInvariant();
            outboundQuery = outboundQuery.Where(x => x.SalesOrder.Channel == channel);
        }
        if (query.ProductVariantId.HasValue)
        {
            outboundQuery = outboundQuery.Where(x =>
                x.OutboundOrderItems.Any(i => i.ProductVariantId == query.ProductVariantId.Value));
        }
        if (query.RiceVarietyId.HasValue)
        {
            outboundQuery = outboundQuery.Where(x =>
                x.OutboundOrderItems.Any(i =>
                    i.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value));
        }

        var allocationRows = await outboundQuery
            .SelectMany(x => x.OutboundOrderItems
                .Where(i => !i.IsDeleted)
                .SelectMany(i => i.Allocations
                    .Where(a => !a.IsDeleted && a.QuantityPicked > 0)
                    .Select(a => new
                    {
                        CompletedAt = x.CompletedDate!.Value,
                        a.QuantityPicked,
                        a.UnitCostPrice,
                        MillingOrderId = a.PaddyLot != null
                            ? a.PaddyLot.SourceMillingOrderId
                            : null,
                        UnitSalePrice = i.SalesOrderItem != null &&
                                        i.SalesOrderItem.QuantityOrdered > 0
                            ? i.SalesOrderItem.LineAmount /
                              i.SalesOrderItem.QuantityOrdered
                            : 0m
                    })))
            .ToListAsync();

        var millingIds = allocationRows
            .Where(x => x.MillingOrderId.HasValue)
            .Select(x => x.MillingOrderId!.Value)
            .Distinct()
            .ToList();
        var millingCosts = await _context.MillingOrders
            .AsNoTracking()
            .Where(x => millingIds.Contains(x.Id))
            .Select(x => new
            {
                x.Id,
                TotalCost = x.TotalCost ?? 0m,
                MaterialCost = x.MillingOrderInputs
                    .Where(i => !i.IsDeleted)
                    .Sum(i => i.ConsumedWeightKg * i.PaddyLot.CostPricePerKg)
            })
            .ToListAsync();
        var costRatios = millingCosts.ToDictionary(
            x => x.Id,
            x => x.TotalCost > 0m
                ? Math.Min(1m, Math.Max(0m, x.MaterialCost / x.TotalCost))
                : 1m);

        var calculated = allocationRows.Select(x =>
        {
            var revenue = x.QuantityPicked * x.UnitSalePrice;
            var cost = x.QuantityPicked * x.UnitCostPrice;
            var paddyRatio = x.MillingOrderId.HasValue
                ? costRatios.GetValueOrDefault(x.MillingOrderId.Value, 1m)
                : 1m;
            return new
            {
                x.CompletedAt,
                Revenue = revenue,
                PaddyCost = cost * paddyRatio,
                MillingCost = cost * (1m - paddyRatio)
            };
        }).ToList();

        var rows = calculated
            .GroupBy(x => new DateTime(x.CompletedAt.Year, x.CompletedAt.Month, 1))
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var revenue = g.Sum(x => x.Revenue);
                var paddyCost = g.Sum(x => x.PaddyCost);
                var millingCost = g.Sum(x => x.MillingCost);
                var profit = revenue - paddyCost - millingCost;
                return new RelativeProfitReportDto
                {
                    PeriodStart = g.Key,
                    PeriodLabel = $"Tháng {g.Key:MM/yyyy}",
                    Revenue = revenue,
                    PaddyCost = paddyCost,
                    MillingCost = millingCost,
                    RelativeProfit = profit,
                    MarginPercent = revenue > 0m ? profit / revenue * 100m : 0m
                };
            })
            .ToList();

        var chart = rows
            .OrderBy(x => x.PeriodStart)
            .Select(x => new ReportChartPointDto
            {
                Label = x.PeriodLabel,
                Value = x.RelativeProfit,
                SecondaryValue = x.Revenue
            })
            .ToList();

        return ApiResponse.Success(ToReportPage(rows, query, chart));
    }

    public async Task<ApiResponse> GetSourceEffectivenessReportAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var receiptQuery = _context.PaddyPurchaseReceipts
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.ReceiptDate >= query.FromDate &&
                        x.ReceiptDate < query.ToDate);
        if (query.WarehouseId.HasValue)
            receiptQuery = receiptQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        if (query.RiceVarietyId.HasValue)
            receiptQuery = receiptQuery.Where(x => x.RiceVarietyId == query.RiceVarietyId.Value);
        if (query.FarmerId.HasValue)
            receiptQuery = receiptQuery.Where(x => x.FarmerId == query.FarmerId.Value);

        var purchases = await receiptQuery
            .Select(x => new
            {
                x.Id,
                x.FarmerId,
                FarmerName = x.Farmer.Name,
                x.ActualWeightKg,
                x.AgreedPrice
            })
            .ToListAsync();

        var receiptIds = purchases.Select(x => x.Id).ToList();
        var riskCounts = await _context.PaddyLots
            .AsNoTracking()
            .Where(x => x.SourceReceiptId.HasValue &&
                        receiptIds.Contains(x.SourceReceiptId.Value) &&
                        !x.IsDeleted &&
                        (x.QualityInspections.Any(q => !q.IsDeleted && !q.PassedInspection) ||
                         !x.Status.IsSellable))
            .GroupBy(x => x.SourceReceipt!.FarmerId)
            .Select(g => new { FarmerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.FarmerId, x => x.Count);

        var inputRows = await _context.MillingOrderInputs
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.PaddyLot.SourceReceiptId.HasValue &&
                        receiptIds.Contains(x.PaddyLot.SourceReceiptId.Value))
            .Select(x => new
            {
                x.MillingOrderId,
                x.PaddyLot.SourceReceipt!.FarmerId,
                x.ConsumedWeightKg
            })
            .ToListAsync();

        var millingIds = inputRows.Select(x => x.MillingOrderId).Distinct().ToList();
        var soldRows = await _context.OutboundOrderItemAllocations
            .AsNoTracking()
            .Where(x => !x.IsDeleted &&
                        x.QuantityPicked > 0 &&
                        x.PaddyLot != null &&
                        x.PaddyLot.SourceMillingOrderId.HasValue &&
                        millingIds.Contains(x.PaddyLot.SourceMillingOrderId.Value) &&
                        x.OutboundOrderItem.OutboundOrder.CompletedDate.HasValue &&
                        x.OutboundOrderItem.OutboundOrder.CompletedDate.Value >= query.FromDate &&
                        x.OutboundOrderItem.OutboundOrder.CompletedDate.Value < query.ToDate)
            .Select(x => new
            {
                MillingOrderId = x.PaddyLot!.SourceMillingOrderId!.Value,
                Revenue = x.QuantityPicked *
                          (x.OutboundOrderItem.SalesOrderItem != null &&
                           x.OutboundOrderItem.SalesOrderItem.QuantityOrdered > 0
                              ? x.OutboundOrderItem.SalesOrderItem.LineAmount /
                                x.OutboundOrderItem.SalesOrderItem.QuantityOrdered
                              : 0m),
                Cost = x.QuantityPicked * x.UnitCostPrice
            })
            .ToListAsync();
        var soldByMilling = soldRows
            .GroupBy(x => x.MillingOrderId)
            .ToDictionary(
                g => g.Key,
                g => (Revenue: g.Sum(x => x.Revenue), Cost: g.Sum(x => x.Cost)));
        var totalInputByMilling = inputRows
            .GroupBy(x => x.MillingOrderId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.ConsumedWeightKg));

        var relatedByFarmer = new Dictionary<int, (decimal Revenue, decimal Profit)>();
        foreach (var input in inputRows)
        {
            if (!soldByMilling.TryGetValue(input.MillingOrderId, out var sold)) continue;
            var totalInput = totalInputByMilling.GetValueOrDefault(input.MillingOrderId);
            if (totalInput <= 0m) continue;
            var fraction = input.ConsumedWeightKg / totalInput;
            relatedByFarmer.TryGetValue(input.FarmerId, out var current);
            relatedByFarmer[input.FarmerId] = (
                current.Revenue + sold.Revenue * fraction,
                current.Profit + (sold.Revenue - sold.Cost) * fraction);
        }

        var rows = purchases
            .GroupBy(x => new { x.FarmerId, x.FarmerName })
            .Select(g =>
            {
                var kg = g.Sum(x => x.ActualWeightKg);
                relatedByFarmer.TryGetValue(g.Key.FarmerId, out var related);
                var risks = riskCounts.GetValueOrDefault(g.Key.FarmerId);
                return new SourceEffectivenessReportDto
                {
                    FarmerId = g.Key.FarmerId,
                    FarmerName = g.Key.FarmerName,
                    PurchasedKg = kg,
                    AveragePurchasePrice = kg > 0m
                        ? g.Sum(x => x.ActualWeightKg * x.AgreedPrice) / kg
                        : 0m,
                    RiskLotCount = risks,
                    RelatedRevenue = related.Revenue,
                    RelativeProfit = related.Profit,
                    Assessment = risks == 0 ? "Không ghi nhận rủi ro" : "Có rủi ro"
                };
            })
            .OrderByDescending(x => x.PurchasedKg)
            .ToList();

        var chart = rows.Select(x => new ReportChartPointDto
        {
            Label = x.FarmerName,
            Value = x.PurchasedKg,
            SecondaryValue = x.RiskLotCount
        }).ToList();

        return ApiResponse.Success(ToReportPage(rows, query, chart));
    }

    public async Task<ApiResponse> GetReportsOverviewAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var summaryResponse = await GetSummaryAsync(query);
        if (!summaryResponse.IsSucceeded || summaryResponse.Resources is not DashboardSummaryDto summary)
            return summaryResponse;

        var profitResponse = await GetRelativeProfitReportAsync(CloneQuery(query, int.MaxValue));
        var profitPage = profitResponse.Resources as ReportPageDto<RelativeProfitReportDto>;
        var sourceResponse = await GetSourceEffectivenessReportAsync(CloneQuery(query, int.MaxValue));
        var sourcePage = sourceResponse.Resources as ReportPageDto<SourceEffectivenessReportDto>;
        var debtResponse = await GetDebtDocumentsReportAsync(CloneQuery(query, int.MaxValue));
        var debtPage = debtResponse.Resources as ReportPageDto<DebtDocumentReportDto>;

        var overview = new ReportOverviewDto
        {
            PaddyOnHandKg = summary.Inventory.PaddyKg,
            RiceOnHandKg = summary.Inventory.RiceKg,
            QuarantinedKg = summary.Inventory.QuarantinedKg,
            PendingDeliveryCount = summary.Sales.PendingDeliveryCount,
            Revenue = summary.Sales.GrossRevenue,
            CustomerReceivable = summary.Debt.CustomerReceivable,
            FarmerPayable = summary.Debt.FarmerPayable,
            RelativeProfit = profitPage?.DataSource.Sum(x => x.RelativeProfit) ?? 0m,
            QualityAlertCount = summary.Alerts.QuarantinedLotCount +
                                summary.Alerts.InspectionOverdueLotCount,
            GoodSourceCount = sourcePage?.DataSource.Count(x => x.RiskLotCount == 0) ?? 0,
            TotalSourceCount = sourcePage?.DataSource.Count ?? 0,
            TopOverdueDebts = debtPage?.DataSource
                .Where(x => x.Status == "OVERDUE")
                .OrderByDescending(x => x.OutstandingAmount)
                .Take(3)
                .Select(x => $"{x.PartyName} quá hạn {x.OutstandingAmount:N0}đ")
                .ToList() ?? new List<string>(),
            OperationalAlerts = summary.RecentAlerts
                .Take(3)
                .Select(x => x.Message)
                .ToList()
        };

        return ApiResponse.Success(overview);
    }

    public async Task<ApiResponse> GetReportFilterOptionsAsync()
    {
        var result = new ReportFilterOptionsDto
        {
            Warehouses = await _context.Warehouses.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.Name, Code = x.Code })
                .ToListAsync(),
            RiceVarieties = await _context.RiceVarieties.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.Name, Code = x.Code })
                .ToListAsync(),
            ProductVariants = await _context.ProductVariants.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.Name, Code = x.SKU })
                .ToListAsync(),
            PaddyLots = await _context.PaddyLots.AsNoTracking()
                .Where(x => !x.IsDeleted && x.RemainingWeightKg > 0)
                .OrderByDescending(x => x.InboundDate)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.LotCode, Code = x.LotType })
                .ToListAsync(),
            Locations = await _context.Locations.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.ZoneName).ThenBy(x => x.SlotCode)
                .Select(x => new ReportOptionDto
                {
                    Id = x.Id,
                    Name = (x.SlotCode ?? x.ZoneName) + " - " + x.Warehouse.Name,
                    Code = x.SlotCode
                })
                .ToListAsync(),
            Farmers = await _context.Farmers.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.Name, Code = x.Code })
                .ToListAsync(),
            Customers = await _context.Customers.AsNoTracking()
                .Where(x => !x.IsDeleted && x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new ReportOptionDto { Id = x.Id, Name = x.Name, Code = x.Code })
                .ToListAsync()
        };

        return ApiResponse.Success(result);
    }

    public async Task<ReportExportDto?> ExportReportAsync(
        string reportType,
        string format,
        DashboardQuery query)
    {
        query.PageIndex = 1;
        query.PageSize = int.MaxValue;
        var response = reportType.Trim().ToLowerInvariant() switch
        {
            "stock" => await GetInventoryByLotReportAsync(query),
            "purchase" => await GetPurchaseReportAsync(query),
            "milling-loss" => await GetMillingYieldReportAsync(query),
            "sales" => await GetSalesRevenueReportAsync(query),
            "two-way-debt" => await GetDebtDocumentsReportAsync(query),
            "quality" => await GetQualityAlertsReportAsync(query),
            "relative-profit" => await GetRelativeProfitReportAsync(query),
            "source-effectiveness" => await GetSourceEffectivenessReportAsync(query),
            _ => new ApiResponse
            {
                IsSucceeded = false,
                Message = "Loại báo cáo không hợp lệ.",
                Status = 400,
                Code = "CMN_400"
            }
        };

        if (!response.IsSucceeded || response.Resources == null) return null;
        var dataSourceProperty = response.Resources.GetType().GetProperty("DataSource");
        if (dataSourceProperty?.GetValue(response.Resources) is not System.Collections.IEnumerable data)
            return null;
        var rows = data.Cast<object>().ToList();
        var normalizedFormat = string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase)
            ? "csv"
            : "xlsx";
        var datePart = DateTimeHelper.VietnamNow().ToString("yyyyMMdd-HHmm");

        return normalizedFormat == "csv"
            ? new ReportExportDto
            {
                Content = BuildCsv(rows),
                ContentType = "text/csv; charset=utf-8",
                FileName = $"bao-cao-{reportType}-{datePart}.csv"
            }
            : new ReportExportDto
            {
                Content = BuildExcel(rows, reportType),
                ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                FileName = $"bao-cao-{reportType}-{datePart}.xlsx"
            };
    }

    public async Task<ApiResponse> GetTodayTasksAsync(DashboardQuery query)
    {
        var today = DateTimeHelper.VietnamNow().Date;
        var todayEnd = today.AddDays(1);
        var tasks = new List<DashboardTaskDto>();

        // 1. Paddy Purchase Schedules
        var scheduleQuery = _context.PaddyPurchaseSchedules
            .Where(x => !x.IsDeleted && x.ScheduleDate >= today && x.ScheduleDate < todayEnd);

        if (query.WarehouseId.HasValue)
        {
            scheduleQuery = scheduleQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var schedules = await scheduleQuery
            .Select(x => new
            {
                x.ScheduleDate,
                FarmerName = x.Farmer.Name,
                RiceVarietyName = x.RiceVariety != null ? x.RiceVariety.Name : "Lúa",
                EstimatedQtyKg = x.EstimatedQtyKg ?? 0,
                StatusCode = x.Status.Code,
                x.Location
            })
            .ToListAsync();

        foreach (var s in schedules)
        {
            var qtyTons = s.EstimatedQtyKg / 1000m;
            tasks.Add(new DashboardTaskDto
            {
                Time = s.ScheduleDate.ToString("HH:mm"),
                Type = "PURCHASE",
                Title = $"Thu mua lúa - {s.FarmerName}",
                Description = $"{s.Location ?? "Địa điểm thu mua"} - ~{qtyTons:0.##} tấn lúa {s.RiceVarietyName}",
                Status = s.StatusCode == LookupCodes.PaddyPurchaseScheduleStatus.New ? "Chờ xử lý" : "Đã xác nhận"
            });
        }

        // 2. Delivery & Retail Delivery Orders
        var salesOrderQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.ExpectedDeliveryDate >= today && x.ExpectedDeliveryDate < todayEnd, false);

        if (query.WarehouseId.HasValue)
        {
            salesOrderQuery = salesOrderQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var salesOrders = await salesOrderQuery
            .Select(x => new
            {
                x.ExpectedDeliveryDate,
                x.SOCode,
                CustomerName = x.Customer.Name,
                StatusName = x.Status.Code,
                x.Channel,
                Items = x.SalesOrderItems.Select(i => new { i.ProductVariant.Name, Quantity = i.QuantityOrdered })
            })
            .ToListAsync();

        foreach (var so in salesOrders)
        {
            var itemsDesc = string.Join(", ", so.Items.Select(i => $"{i.Name} x{i.Quantity:0.##}"));
            var isWholesale = so.Channel == LookupCodes.SalesOrderChannel.Wholesale;
            tasks.Add(new DashboardTaskDto
            {
                Time = so.ExpectedDeliveryDate.HasValue ? so.ExpectedDeliveryDate.Value.ToString("HH:mm") : "00:00",
                Type = "DELIVERY",
                Title = isWholesale ? $"Giao hàng - {so.SOCode}" : $"Giao lẻ - {so.SOCode}",
                Description = $"{so.CustomerName} - {itemsDesc}",
                Status = (so.StatusName == SalesOrderStatusNames.New || so.StatusName == SalesOrderStatusNames.PendingConfirm) 
                    ? "Chờ xử lý" 
                    : "Đã xác nhận"
            });
        }

        // 3. Inspections / Quality Alerts
        var alertQuery = _alertRepository.FindByCondition(x => !x.IsDeleted && x.Status == AlertConstants.Status.Open && x.RelatedEntityType == AlertConstants.RelatedEntityType.PaddyLot, false);
        if (query.WarehouseId.HasValue)
        {
            alertQuery = alertQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var qualityAlerts = await alertQuery
            .Select(x => new
            {
                x.CreatedDate,
                LotCode = _context.PaddyLots.Where(p => p.Id == x.RelatedEntityId).Select(p => p.LotCode).FirstOrDefault() ?? "Lô lúa",
                x.Message
            })
            .ToListAsync();

        foreach (var a in qualityAlerts)
        {
            tasks.Add(new DashboardTaskDto
            {
                Time = a.CreatedDate.ToString("HH:mm"),
                Type = "INSPECTION",
                Title = $"Kiểm tra lô lúa {a.LotCode}",
                Description = a.Message,
                Status = "Chờ xử lý"
            });
        }

        return ApiResponse.Success(tasks.OrderBy(t => t.Time).ToList());
    }

    public async Task<ApiResponse> GetPurchaseChartAsync(DashboardQuery query)
    {
        var period = (query.Period ?? "today").Trim().ToLowerInvariant();
        var today = DateTimeHelper.VietnamNow().Date;

        // Xây các "khoảng" (bucket) tùy theo mốc thời gian: mỗi bucket = 1 cột trên biểu đồ.
        var buckets = new List<(string Label, DateTime Start, DateTime End)>();

        if (period == "year")
        {
            // 12 tháng gần nhất, kết thúc ở tháng hiện tại → cột "T1".."T12".
            var firstMonth = new DateTime(today.Year, today.Month, 1).AddMonths(-11);
            for (var i = 0; i < 12; i++)
            {
                var s = firstMonth.AddMonths(i);
                buckets.Add(($"T{s.Month}", s, s.AddMonths(1)));
            }
        }
        else if (period == "month")
        {
            // 4 tuần gần nhất (mỗi tuần 7 ngày), kết thúc hôm nay → cột "Tuần 1".."Tuần 4".
            var start = today.AddDays(1).AddDays(-28);
            for (var i = 0; i < 4; i++)
            {
                var s = start.AddDays(i * 7);
                buckets.Add(($"Tuần {i + 1}", s, s.AddDays(7)));
            }
        }
        else
        {
            // 7 ngày gần nhất → cột "T2".."CN".
            var start = today.AddDays(-6);
            for (var i = 0; i < 7; i++)
            {
                var d = start.AddDays(i);
                buckets.Add((GetVietnameseDayOfWeek(d.DayOfWeek), d, d.AddDays(1)));
            }
        }

        var rangeStart = buckets[0].Start;
        var rangeEnd = buckets[^1].End;

        var receiptQuery = _context.PaddyPurchaseReceipts
            .Where(x => !x.IsDeleted && x.ReceiptDate >= rangeStart && x.ReceiptDate < rangeEnd);

        if (query.WarehouseId.HasValue)
        {
            receiptQuery = receiptQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var rawData = await receiptQuery
            .Select(x => new
            {
                x.ReceiptDate,
                x.ActualWeightKg,
                x.AgreedPrice
            })
            .ToListAsync();

        var chartData = buckets.Select(b =>
        {
            var rows = rawData.Where(r => r.ReceiptDate >= b.Start && r.ReceiptDate < b.End).ToList();
            var weightSum = rows.Sum(r => r.ActualWeightKg);
            var avgPrice = weightSum > 0
                ? rows.Sum(r => r.AgreedPrice * r.ActualWeightKg) / weightSum
                : 0m;

            return new ChartDataPointDto
            {
                DayOfWeek = b.Label,
                VolumeTons = weightSum / 1000m,
                AveragePrice = avgPrice
            };
        }).ToList();

        return ApiResponse.Success(chartData);
    }

    private string GetVietnameseDayOfWeek(DayOfWeek day)
    {
        return day switch
        {
            DayOfWeek.Monday => "T2",
            DayOfWeek.Tuesday => "T3",
            DayOfWeek.Wednesday => "T4",
            DayOfWeek.Thursday => "T5",
            DayOfWeek.Friday => "T6",
            DayOfWeek.Saturday => "T7",
            DayOfWeek.Sunday => "CN",
            _ => string.Empty
        };
    }

    public async Task<ApiResponse> GetOperationalEfficiencyAsync(DashboardQuery query)
    {
        var dateValidation = ValidateAndPopulateDates(query);
        if (dateValidation != null) return dateValidation;

        var metrics = await GetOperationalEfficiencyInternalAsync(query);
        return ApiResponse.Success(metrics);
    }

    private async Task<EfficiencyMetricsDto> GetOperationalEfficiencyInternalAsync(DashboardQuery query)
    {
        var fromDate = query.FromDate;
        var toDate = query.ToDate;

        // 1. Tỷ lệ giao đúng hạn
        var completedStatusId = await GetCompletedSalesOrderStatusIdAsync();
        var salesQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.StatusId == completedStatusId &&
                                  x.OrderDate >= fromDate && x.OrderDate < toDate, false);

        if (query.WarehouseId.HasValue)
        {
            salesQuery = salesQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var completedOrders = await salesQuery
            .Select(x => new
            {
                x.ExpectedDeliveryDate,
                MaxOutboundCompletedDate = x.OutboundOrders
                    .Where(o => !o.IsDeleted && o.CompletedDate.HasValue)
                    .Max(o => (DateTime?)o.CompletedDate)
            })
            .ToListAsync();

        decimal onTimeRate = 0m;
        if (completedOrders.Count > 0)
        {
            var onTimeCount = completedOrders.Count(x =>
                x.ExpectedDeliveryDate.HasValue &&
                (!x.MaxOutboundCompletedDate.HasValue || x.MaxOutboundCompletedDate.Value <= x.ExpectedDeliveryDate.Value)
            );
            onTimeRate = (decimal)onTimeCount / completedOrders.Count * 100m;
        }

        // 2. Thu hồi công nợ
        var debtTransactions = await _debtTransactionRepository
            .FindByCondition(x => !x.IsDeleted && x.TransactionDate >= fromDate && x.TransactionDate < toDate, false)
            .ToListAsync();

        decimal debtRecoveryRate = 0m;
        var payments = debtTransactions.Where(x => x.TransactionType == LookupCodes.DebtTransactionType.Payment).Sum(x => x.Amount);
        var charges = debtTransactions.Where(x => x.TransactionType == LookupCodes.DebtTransactionType.Charge).Sum(x => x.Amount);
        if (payments + charges > 0)
        {
            debtRecoveryRate = payments / (payments + charges) * 100m;
        }

        // 3. Hao hụt kho
        var millingQuery = _millingOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.CompletedAt.HasValue &&
                                  x.CompletedAt.Value >= fromDate && x.CompletedAt.Value < toDate, false);

        if (query.WarehouseId.HasValue)
        {
            millingQuery = millingQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var millingData = await millingQuery
            .Select(x => new { x.ComputedPaddyKg, LossKg = x.LossKg ?? 0 })
            .ToListAsync();

        decimal lossRate = 0m;
        var totalPaddy = millingData.Sum(x => x.ComputedPaddyKg);
        var totalLoss = millingData.Sum(x => x.LossKg);
        if (totalPaddy > 0)
        {
            lossRate = totalLoss / totalPaddy * 100m;
        }

        return new EfficiencyMetricsDto
        {
            OnTimeDeliveryRate = Math.Round(onTimeRate, 1),
            OnTimeDeliveryTarget = 95.0m,
            DebtRecoveryRate = Math.Round(debtRecoveryRate, 1),
            WarehouseLossRate = Math.Round(lossRate, 1),
            WarehouseLossTarget = 1.0m
        };
    }

    public async Task<ApiResponse> GetRecentAlertsAsync(DashboardQuery query)
    {
        var alertsQuery = _alertRepository.FindByCondition(x => !x.IsDeleted && x.Status == AlertConstants.Status.Open, false);

        if (query.WarehouseId.HasValue)
        {
            alertsQuery = alertsQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        // Trả tối đa 30 cảnh báo mới nhất để FE phân trang (5/trang) tại màn Tổng quan.
        var alerts = await alertsQuery
            .OrderByDescending(x => x.CreatedDate)
            .Take(30)
            .Select(x => new AlertItemDto
            {
                Id = x.Id,
                Message = x.Message,
                Severity = x.Severity,
                CreatedAt = x.CreatedDate
            })
            .ToListAsync();

        foreach (var a in alerts)
        {
            a.TimeAgo = GetTimeAgo(a.CreatedAt);
        }

        return ApiResponse.Success(alerts);
    }

    private string GetTimeAgo(DateTime dateTime)
    {
        var now = DateTimeHelper.VietnamNow();
        var diff = now - dateTime;
        if (diff.TotalSeconds < 0) return "Vừa xong";
        if (diff.TotalMinutes < 1) return "Vừa xong";
        if (diff.TotalMinutes < 60) return $"{(int)diff.TotalMinutes}p trước";
        if (diff.TotalHours < 24) return $"{(int)diff.TotalHours}h trước";
        if (dateTime.Date == now.Date.AddDays(-1)) return "Hôm qua";
        if (dateTime.Date == now.Date) return "Hôm nay";
        return dateTime.ToString("dd/MM/yyyy");
    }

    private static ReportPageDto<T> ToReportPage<T>(
        List<T> rows,
        DashboardQuery query,
        List<ReportChartPointDto>? chart = null,
        object? summary = null)
    {
        var pageIndex = Math.Max(1, query.PageIndex);
        var pageSize = query.PageSize <= 0 ? 20 : query.PageSize;
        var total = rows.Count;
        List<T> pageRows;
        if (pageSize == int.MaxValue)
        {
            pageRows = rows;
            pageIndex = 1;
        }
        else
        {
            pageSize = Math.Min(pageSize, 200);
            var skip = (pageIndex - 1) * pageSize;
            pageRows = skip >= total
                ? new List<T>()
                : rows.Skip(skip).Take(pageSize).ToList();
        }

        return new ReportPageDto<T>
        {
            DataSource = pageRows,
            Total = total,
            TotalFiltered = total,
            CurrentPage = pageIndex,
            PageSize = pageSize,
            Chart = chart ?? new List<ReportChartPointDto>(),
            Summary = summary
        };
    }

    private static DashboardQuery CloneQuery(DashboardQuery source, int pageSize)
    {
        return new DashboardQuery
        {
            FromDate = source.FromDate,
            ToDate = source.ToDate,
            WarehouseId = source.WarehouseId,
            RiceVarietyId = source.RiceVarietyId,
            ProductVariantId = source.ProductVariantId,
            PaddyLotId = source.PaddyLotId,
            LocationId = source.LocationId,
            FarmerId = source.FarmerId,
            CustomerId = source.CustomerId,
            ProductType = source.ProductType,
            Channel = source.Channel,
            Status = source.Status,
            Period = source.Period,
            PageIndex = 1,
            PageSize = pageSize,
            SortBy = source.SortBy,
            SortDirection = source.SortDirection
        };
    }

    private static byte[] BuildCsv(List<object> rows)
    {
        var encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        if (rows.Count == 0)
            return encoding.GetBytes("Không có dữ liệu");

        var properties = GetExportProperties(rows[0].GetType());
        var builder = new StringBuilder();
        builder.AppendLine(string.Join(",", properties.Select(x => EscapeCsv(x.Name))));
        foreach (var row in rows)
        {
            builder.AppendLine(string.Join(
                ",",
                properties.Select(x => EscapeCsv(FormatExportValue(x.GetValue(row))))));
        }
        return encoding.GetPreamble().Concat(encoding.GetBytes(builder.ToString())).ToArray();
    }

    private static byte[] BuildExcel(List<object> rows, string sheetName)
    {
        IWorkbook workbook = new XSSFWorkbook();
        var safeSheetName = new string((sheetName ?? "Report")
            .Where(c => !"[]:*?/\\".Contains(c))
            .Take(31)
            .ToArray());
        var sheet = workbook.CreateSheet(string.IsNullOrWhiteSpace(safeSheetName)
            ? "Report"
            : safeSheetName);

        if (rows.Count == 0)
        {
            sheet.CreateRow(0).CreateCell(0).SetCellValue("Không có dữ liệu");
        }
        else
        {
            var properties = GetExportProperties(rows[0].GetType());
            var headerStyle = workbook.CreateCellStyle();
            var headerFont = workbook.CreateFont();
            headerFont.IsBold = true;
            headerStyle.SetFont(headerFont);

            var header = sheet.CreateRow(0);
            for (var i = 0; i < properties.Length; i++)
            {
                var cell = header.CreateCell(i);
                cell.SetCellValue(properties[i].Name);
                cell.CellStyle = headerStyle;
            }

            for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                var excelRow = sheet.CreateRow(rowIndex + 1);
                for (var columnIndex = 0; columnIndex < properties.Length; columnIndex++)
                {
                    var value = properties[columnIndex].GetValue(rows[rowIndex]);
                    var cell = excelRow.CreateCell(columnIndex);
                    switch (value)
                    {
                        case null:
                            cell.SetCellValue(string.Empty);
                            break;
                        case DateTime date:
                            cell.SetCellValue(date.ToString("dd/MM/yyyy HH:mm"));
                            break;
                        case decimal number:
                            cell.SetCellValue((double)number);
                            break;
                        case int number:
                            cell.SetCellValue(number);
                            break;
                        case long number:
                            cell.SetCellValue((double)number);
                            break;
                        case bool boolean:
                            cell.SetCellValue(boolean ? "Có" : "Không");
                            break;
                        default:
                            cell.SetCellValue(value.ToString());
                            break;
                    }
                }
            }

            for (var i = 0; i < properties.Length; i++)
            {
                sheet.AutoSizeColumn(i);
                var currentWidth = sheet.GetColumnWidth(i);
                sheet.SetColumnWidth(i, Math.Min(currentWidth + 512, 15000));
            }
        }

        using var stream = new MemoryStream();
        workbook.Write(stream, true);
        return stream.ToArray();
    }

    private static PropertyInfo[] GetExportProperties(Type type)
        => type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(x => x.CanRead &&
                        (x.PropertyType == typeof(string) ||
                         !typeof(System.Collections.IEnumerable).IsAssignableFrom(x.PropertyType)) &&
                        x.PropertyType != typeof(byte[]))
            .ToArray();

    private static string FormatExportValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime date => date.ToString("dd/MM/yyyy HH:mm"),
            decimal number => number.ToString("0.###", CultureInfo.InvariantCulture),
            bool boolean => boolean ? "Có" : "Không",
            _ => value.ToString() ?? string.Empty
        };
    }

    private static string EscapeCsv(string value)
    {
        if (value.Contains('"')) value = value.Replace("\"", "\"\"");
        return value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0
            ? $"\"{value}\""
            : value;
    }

    private ApiResponse? ValidateAndPopulateDates(DashboardQuery query)
    {
        if (query.FromDate == default)
        {
            var now = DateTimeHelper.VietnamNow();
            query.FromDate = new DateTime(now.Year, now.Month, 1);
        }
        if (query.ToDate == default)
        {
            query.ToDate = DateTimeHelper.VietnamNow();
        }
        if (query.FromDate > query.ToDate)
        {
            return new ApiResponse
            {
                IsSucceeded = false,
                Message = "FromDate không được lớn hơn ToDate.",
                Status = 400,
                Code = "DASHBOARD_400_01"
            };
        }
        // Query từ input type=date gửi lên lúc 00:00. Chuyển ToDate thành mốc
        // độc quyền đầu ngày kế tiếp để ngày kết thúc vẫn được tính đầy đủ.
        if (query.ToDate.TimeOfDay == TimeSpan.Zero)
        {
            query.ToDate = query.ToDate.Date.AddDays(1);
        }
        return null;
    }
}
