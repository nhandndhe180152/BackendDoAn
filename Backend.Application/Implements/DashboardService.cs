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
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DashboardService> _logger;
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
        ILogger<DashboardService> logger,
        IHttpContextAccessor httpContextAccessor,
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
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    // H2: resolve Completed status ID by Name to avoid magic number
    private int? _completedSalesStatusId;
    private async Task<int> GetCompletedSalesOrderStatusIdAsync()
    {
        if (_completedSalesStatusId.HasValue) return _completedSalesStatusId.Value;
        var status = await _salesOrderStatusRepository.FirstOrDefaultAsync(
            x => x.Name == SalesOrderStatusNames.Completed && !x.IsDeleted);
        _completedSalesStatusId = status?.Id
            ?? throw new InvalidOperationException($"SalesOrderStatus '{SalesOrderStatusNames.Completed}' not found.");
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
        var chargesByDebtId = debtIdsWithBalance.Count > 0
            ? await _debtTransactionRepository
                .FindByCondition(x =>
                    !x.IsDeleted &&
                    x.TransactionType == LookupCodes.DebtTransactionType.Charge &&
                    debtIdsWithBalance.Contains(x.PartyDebtId), false)
                .OrderByDescending(x => x.TransactionDate)
                .ToListAsync()
            : new List<DebtTransaction>();

        var chargeMap = chargesByDebtId.GroupBy(x => x.PartyDebtId)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var debt in debts.Where(x => x.CurrentBalance > 0))
        {
            decimal overdue = 0;
            if (chargeMap.TryGetValue(debt.Id, out var charges))
            {
                decimal remaining = debt.CurrentBalance;
                foreach (var charge in charges)
                {
                    if (remaining <= 0) break;
                    var unpaidAmount = Math.Min(charge.Amount, remaining);
                    if (charge.DueDate.HasValue && charge.DueDate.Value < nowLimit)
                    {
                        overdue += unpaidAmount;
                    }
                    remaining -= unpaidAmount;
                }
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
                                 (!x.QualityInspections.Any() || x.QualityInspections.Max(q => q.InspectedAt) < thirtyDaysAgo), false)
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
            .Where(x => pendingDeliveryStatuses.Contains(x.Status.Name) || x.Status.Name == SalesOrderStatusNames.New)
            .Select(x => new { x.Status.Name })
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

        var lots = await baseQuery
            .Select(x => new
            {
                x.Id,
                x.LotCode,
                x.LotType,
                RiceVarietyName = x.RiceVariety != null ? x.RiceVariety.Name : null,
                ProductVariantName = x.ProductVariant.Name,
                WarehouseName = x.Warehouse.Name,
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
                LotCode = lot.LotCode,
                LotType = lot.LotType,
                RiceVariety = lot.RiceVarietyName,
                ProductVariant = lot.ProductVariantName,
                Warehouse = lot.WarehouseName,
                Location = locationsStr,
                Locations = locationsStr,
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
        }).ToList();

        return ApiResponse.Success(report);
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
        var chargesByDebtId = await _debtTransactionRepository
            .FindByCondition(x =>
                !x.IsDeleted &&
                x.TransactionType == LookupCodes.DebtTransactionType.Charge &&
                debtIdsWithBalance.Contains(x.PartyDebtId), false)
            .OrderByDescending(x => x.TransactionDate)
            .ToListAsync();

        var chargeMap = chargesByDebtId.GroupBy(x => x.PartyDebtId)
            .ToDictionary(g => g.Key, g => g.ToList());

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
            if (debt.CurrentBalance > 0 && chargeMap.TryGetValue(debt.Id, out var charges))
            {
                decimal remaining = debt.CurrentBalance;
                foreach (var charge in charges)
                {
                    if (remaining <= 0) break;
                    var unpaidAmount = Math.Min(charge.Amount, remaining);
                    if (charge.DueDate.HasValue && charge.DueDate.Value < nowLimit)
                        overdue += unpaidAmount;
                    remaining -= unpaidAmount;
                }
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

    public async Task<ApiResponse> GetMillingYieldReportAsync(DashboardQuery query)
    {
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

        var list = await baseQuery
            .Select(x => new MillingYieldReportDto
            {
                MillingCode       = x.MillingCode,
                WarehouseName     = x.Warehouse.Name,
                ComputedPaddyKg   = x.ComputedPaddyKg,
                TotalRiceOutputKg = x.TotalRiceOutputKg,
                ActualYieldRate   = x.ComputedPaddyKg > 0 ? x.TotalRiceOutputKg / x.ComputedPaddyKg : 0,
                LossKg            = x.LossKg ?? 0,
                CompletedAt       = x.CompletedAt
            })
            .ToListAsync();

        return ApiResponse.Success(list);
    }

    public async Task<ApiResponse> GetSalesRevenueReportAsync(DashboardQuery query)
    {
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

        var orders = await baseQuery
            .Select(x => new
            {
                x.Id,
                x.OrderDate,
                x.SOCode,
                CustomerName = x.Customer.Name,
                x.TotalAmount,
                DepositAmount = x.DepositAmount ?? 0,
                StatusName = x.Status.Name
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
                OrderDate         = o.OrderDate,
                SOCode            = o.SOCode,
                CustomerName      = o.CustomerName,
                TotalAmount       = o.TotalAmount,
                DepositAmount     = o.DepositAmount,
                AmountCollected   = collected,
                OutstandingAmount = Math.Max(0, o.TotalAmount - collected),
                StatusName        = o.StatusName
            };
        }).ToList();

        return ApiResponse.Success(report);
    }

    public async Task<ApiResponse> GetQualityAlertsReportAsync(DashboardQuery query)
    {
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

        var rawLots = await baseQuery
            .Select(x => new
            {
                x.LotCode,
                ProductVariantName = x.ProductVariant.Name,
                WarehouseName = x.Warehouse.Name,
                x.RemainingWeightKg,
                x.QualityStatus,
                IsSellable = x.Status.IsSellable,
                LastInspectionAt = x.QualityInspections
                    .Where(q => !q.IsDeleted)
                    .OrderByDescending(q => q.InspectedAt)
                    .Select(q => (DateTime?)q.InspectedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var list = rawLots
            .Select(x =>
            {
                var isOverdue = !x.LastInspectionAt.HasValue || x.LastInspectionAt.Value < thirtyDaysAgo;
                return new QualityAlertReportDto
                {
                    LotCode            = x.LotCode,
                    ProductVariantName = x.ProductVariantName,
                    WarehouseName      = x.WarehouseName,
                    RemainingWeightKg  = x.RemainingWeightKg,
                    QualityStatus      = x.QualityStatus,
                    LastInspectionAt   = x.LastInspectionAt,
                    IsQuarantined      = !x.IsSellable,
                    IsInspectionOverdue = isOverdue
                };
            })
            .Where(x => x.IsQuarantined || x.IsInspectionOverdue)
            .ToList();

        return ApiResponse.Success(list);
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
                Status = s.StatusCode == "NEW" ? "Chờ xử lý" : "Đã xác nhận"
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
                StatusName = x.Status.Name,
                x.Channel,
                Items = x.SalesOrderItems.Select(i => new { i.ProductVariant.Name, Quantity = i.QuantityOrdered })
            })
            .ToListAsync();

        foreach (var so in salesOrders)
        {
            var itemsDesc = string.Join(", ", so.Items.Select(i => $"{i.Name} x{i.Quantity:0.##}"));
            var isWholesale = so.Channel == "WHOLESALE";
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
        var today = DateTimeHelper.VietnamNow().Date;
        var start = today.AddDays(-6);
        var end = today.AddDays(1);

        var receiptQuery = _context.PaddyPurchaseReceipts
            .Where(x => !x.IsDeleted && x.ReceiptDate >= start && x.ReceiptDate < end);

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

        var days = Enumerable.Range(0, 7)
            .Select(i => start.AddDays(i))
            .ToList();

        var chartData = days.Select(d =>
        {
            var dayReceipts = rawData.Where(r => r.ReceiptDate.Date == d.Date).ToList();
            var volumeTons = dayReceipts.Sum(r => r.ActualWeightKg) / 1000m;
            var avgPrice = dayReceipts.Count > 0 ? dayReceipts.Average(r => r.AgreedPrice) : 0m;

            return new ChartDataPointDto
            {
                DayOfWeek = GetVietnameseDayOfWeek(d.DayOfWeek),
                VolumeTons = volumeTons,
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

        var alerts = await alertsQuery
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
}