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
    private readonly IRepositoryBase<PartyDebt, int> _partyDebtRepository;
    private readonly IRepositoryBase<DebtTransaction, int> _debtTransactionRepository;
    private readonly IRepositoryBase<Alert, int> _alertRepository;
    private readonly IRepositoryBase<Customer, int> _customerRepository;
    private readonly IRepositoryBase<Farmer, int> _farmerRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<DashboardService> _logger;

    public DashboardService(
        IRepositoryBase<Inventory, int> inventoryRepository,
        IRepositoryBase<PaddyLot, int> paddyLotRepository,
        IRepositoryBase<MillingOrder, int> millingOrderRepository,
        IRepositoryBase<SalesOrder, int> salesOrderRepository,
        IRepositoryBase<PartyDebt, int> partyDebtRepository,
        IRepositoryBase<DebtTransaction, int> debtTransactionRepository,
        IRepositoryBase<Alert, int> alertRepository,
        IRepositoryBase<Customer, int> customerRepository,
        IRepositoryBase<Farmer, int> farmerRepository,
        ILogger<DashboardService> logger,
        IHttpContextAccessor httpContextAccessor)
    {
        _inventoryRepository = inventoryRepository;
        _paddyLotRepository = paddyLotRepository;
        _millingOrderRepository = millingOrderRepository;
        _salesOrderRepository = salesOrderRepository;
        _partyDebtRepository = partyDebtRepository;
        _debtTransactionRepository = debtTransactionRepository;
        _alertRepository = alertRepository;
        _customerRepository = customerRepository;
        _farmerRepository = farmerRepository;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
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

        // 1. Inventory Summary
        var invQuery = _inventoryRepository.FindByCondition(x => !x.IsDeleted, false);

        if (query.WarehouseId.HasValue)
        {
            invQuery = invQuery.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }
        if (query.RiceVarietyId.HasValue)
        {
            invQuery = invQuery.Where(x => x.ProductVariant.RiceVarietyId == query.RiceVarietyId.Value ||
                                           (x.PaddyLot != null && x.PaddyLot.RiceVarietyId == query.RiceVarietyId.Value));
        }

        var invData = await invQuery
            .Select(x => new
            {
                x.QuantityOnHand,
                x.QuantityReserved,
                IsQuarantined = x.PaddyLot != null && !x.PaddyLot.Status.IsSellable,
                LotType = x.PaddyLot != null ? x.PaddyLot.LotType : null,
                IsVariantByproduct = x.ProductVariant.IsByproduct
            })
            .ToListAsync();

        var inventorySummary = new InventorySummaryDto
        {
            OnHandKg      = invData.Sum(x => x.QuantityOnHand),
            ReservedKg    = invData.Sum(x => x.QuantityReserved),
            QuarantinedKg = invData.Where(x => x.IsQuarantined).Sum(x => x.QuantityOnHand),
            PaddyKg       = invData.Where(x => x.LotType == "PADDY").Sum(x => x.QuantityOnHand),
            RiceKg        = invData.Where(x => x.LotType == "RICE").Sum(x => x.QuantityOnHand),
            ByproductKg   = invData.Where(x => x.LotType == "BYPRODUCT" || (x.LotType == null && x.IsVariantByproduct)).Sum(x => x.QuantityOnHand)
        };
        inventorySummary.AvailableKg = Math.Max(0, inventorySummary.OnHandKg - inventorySummary.ReservedKg - inventorySummary.QuarantinedKg);

        // 2. Debt Summary
        var debts = await _partyDebtRepository
            .FindByCondition(x => !x.IsDeleted && x.IsActive, false)
            .ToListAsync();

        var farmerPayable      = debts.Where(x => x.PartyType == "FARMER" && x.Direction == "PAYABLE").Sum(x => x.CurrentBalance);
        var customerReceivable = debts.Where(x => x.PartyType == "CUSTOMER" && x.Direction == "RECEIVABLE").Sum(x => x.CurrentBalance);

        decimal overduePayable = 0;
        decimal overdueReceivable = 0;
        var nowLimit = DateTimeHelper.VietnamNow();

        foreach (var debt in debts.Where(x => x.CurrentBalance > 0))
        {
            var charges = await _debtTransactionRepository
                .FindByCondition(x => !x.IsDeleted && x.PartyDebtId == debt.Id && x.TransactionType == "CHARGE", false)
                .OrderByDescending(x => x.TransactionDate)
                .ToListAsync();

            decimal remaining = debt.CurrentBalance;
            decimal overdue = 0;

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

            if (debt.Direction == "PAYABLE") overduePayable += overdue;
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

        // 4. Sales Summary
        var salesQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.StatusId == 7 &&
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
            .FindByCondition(x => !x.IsDeleted && x.TransactionType == "PAYMENT" &&
                                  x.RefType == "SALES_ORDER" && x.RefId.HasValue &&
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
        var alertsQuery = _alertRepository.FindByCondition(x => !x.IsDeleted && x.Status == "OPEN", false);

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

        var alertsSummary = new AlertsSummaryDto
        {
            OpenCount                 = openAlerts.Count,
            CriticalCount             = openAlerts.Count(x => x.Severity == "CRITICAL"),
            QuarantinedLotCount       = quarantinedLotCount,
            InspectionOverdueLotCount = overdueLotCount
        };

        return ApiResponse.Success(new DashboardSummaryDto
        {
            Inventory = inventorySummary,
            Debt      = debtSummary,
            Milling   = millingSummary,
            Sales     = salesSummary,
            Alerts    = alertsSummary
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

        var rawList = await baseQuery
            .Select(x => new
            {
                x.LotCode,
                x.LotType,
                RiceVarietyName = x.RiceVariety != null ? x.RiceVariety.Name : null,
                ProductVariantName = x.ProductVariant.Name,
                WarehouseName = x.Warehouse.Name,
                LocationCode = x.Location != null ? x.Location.SlotCode : null,
                x.InboundDate,
                x.InitialWeightKg,
                x.RemainingWeightKg,
                StatusName = x.Status.Name,
                IsSellable = x.Status.IsSellable,
                LastInspectionAt = x.QualityInspections
                    .Where(q => !q.IsDeleted)
                    .OrderByDescending(q => q.InspectedAt)
                    .Select(q => (DateTime?)q.InspectedAt)
                    .FirstOrDefault()
            })
            .ToListAsync();

        var report = rawList.Select(x =>
        {
            var isQuarantined = !x.IsSellable;
            return new InventoryByLotReportDto
            {
                LotCode           = x.LotCode,
                LotType           = x.LotType,
                RiceVariety       = x.RiceVarietyName,
                ProductVariant    = x.ProductVariantName,
                Warehouse         = x.WarehouseName,
                Location          = x.LocationCode,
                InboundDate       = x.InboundDate,
                InitialWeightKg   = x.InitialWeightKg,
                RemainingWeightKg = x.RemainingWeightKg,
                OnHandKg          = x.RemainingWeightKg,
                ReservedKg        = 0,
                QuarantinedKg     = isQuarantined ? x.RemainingWeightKg : 0,
                AvailableKg       = isQuarantined ? 0 : x.RemainingWeightKg,
                LastInspectionAt  = x.LastInspectionAt,
                LotStatus         = x.StatusName
            };
        }).ToList();

        return ApiResponse.Success(report);
    }

    public async Task<ApiResponse> GetInventoryByWarehouseReportAsync(DashboardQuery query)
    {
        var inventories = _inventoryRepository.FindByCondition(x => !x.IsDeleted, false);

        if (query.WarehouseId.HasValue)
        {
            inventories = inventories.Where(x => x.WarehouseId == query.WarehouseId.Value);
        }

        var data = await inventories
            .Select(x => new
            {
                x.WarehouseId,
                WarehouseName = x.Warehouse.Name,
                x.QuantityOnHand,
                x.QuantityReserved,
                LotType = x.PaddyLot != null ? x.PaddyLot.LotType : null,
                IsVariantByproduct = x.ProductVariant.IsByproduct
            })
            .ToListAsync();

        var grouped = data
            .GroupBy(x => new { x.WarehouseId, x.WarehouseName })
            .Select(g => new InventoryByWarehouseReportDto
            {
                WarehouseId   = g.Key.WarehouseId,
                WarehouseName = g.Key.WarehouseName,
                OnHandKg      = g.Sum(x => x.QuantityOnHand),
                ReservedKg    = g.Sum(x => x.QuantityReserved),
                AvailableKg   = Math.Max(0, g.Sum(x => x.QuantityOnHand) - g.Sum(x => x.QuantityReserved)),
                PaddyKg       = g.Where(x => x.LotType == "PADDY").Sum(x => x.QuantityOnHand),
                RiceKg        = g.Where(x => x.LotType == "RICE").Sum(x => x.QuantityOnHand),
                ByproductKg   = g.Where(x => x.LotType == "BYPRODUCT" || (x.LotType == null && x.IsVariantByproduct)).Sum(x => x.QuantityOnHand)
            })
            .ToList();

        return ApiResponse.Success(grouped);
    }

    public async Task<ApiResponse> GetTwoWayDebtReportAsync(DashboardQuery query)
    {
        var debts = await _partyDebtRepository
            .FindByCondition(x => !x.IsDeleted && x.IsActive, false)
            .ToListAsync();

        var report = new List<TwoWayDebtReportDto>();
        var nowLimit = DateTimeHelper.VietnamNow();

        foreach (var debt in debts)
        {
            var name = "";
            if (debt.PartyType == "CUSTOMER")
            {
                var cust = await _customerRepository.GetByIdAsync(debt.PartyId);
                name = cust?.Name ?? $"Khách hàng {debt.PartyId}";
            }
            else if (debt.PartyType == "FARMER")
            {
                var farmer = await _farmerRepository.GetByIdAsync(debt.PartyId);
                name = farmer?.Name ?? $"Nông dân {debt.PartyId}";
            }

            decimal overdue = 0;
            if (debt.CurrentBalance > 0)
            {
                var charges = await _debtTransactionRepository
                    .FindByCondition(x => !x.IsDeleted && x.PartyDebtId == debt.Id && x.TransactionType == "CHARGE", false)
                    .OrderByDescending(x => x.TransactionDate)
                    .ToListAsync();

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
        var baseQuery = _salesOrderRepository
            .FindByCondition(x => !x.IsDeleted && x.StatusId == 7 &&
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
            .FindByCondition(x => !x.IsDeleted && x.TransactionType == "PAYMENT" &&
                                  x.RefType == "SALES_ORDER" && x.RefId.HasValue &&
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
}