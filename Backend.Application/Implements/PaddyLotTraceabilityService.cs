using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.InboundOrders;
using Backend.Application.DTOs.PaddyLots;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Constants;
using Backend.Share.Entities;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Backend.Application.Implements;

public class PaddyLotTraceabilityService : IPaddyLotTraceabilityService
{
    private static readonly JsonSerializerOptions PurchaseBagJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<PaddyLotTraceabilityService> _logger;

    public PaddyLotTraceabilityService(
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        ILoggerFactory loggerFactory)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _logger = loggerFactory.CreateLogger<PaddyLotTraceabilityService>();
    }

    public async Task<ApiResponse> GetByLotIdAsync(
        int lotId,
        bool includeTimeline = true,
        bool includeQuality = true,
        bool includeMilling = true,
        bool includeOutbound = true,
        int maxDepth = 10,
        CancellationToken cancellationToken = default)
    {
        if (lotId <= 0)
        {
            return ApiResponse.BadRequest(message: "ID lô hàng không hợp lệ.", code: "LOT_ID_INVALID");
        }

        if (maxDepth <= 0 || maxDepth > 50)
        {
            return ApiResponse.BadRequest(message: "Độ sâu truy vết không hợp lệ.", code: "TRACEABILITY_MAX_DEPTH_INVALID");
        }

        var requestedLot = await _context.PaddyLots
            .AsNoTracking()
            .Include(x => x.ProductVariant)
            .Include(x => x.RiceVariety)
            .Include(x => x.Status)
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => x.Id == lotId && !x.IsDeleted, cancellationToken);

        if (requestedLot == null)
        {
            return ApiResponse.NotFound(message: "Không tìm thấy lô cần truy vết.", code: "PADDY_LOT_NOT_FOUND");
        }

        var accessError = CheckOrganizationAccess(requestedLot);
        if (accessError != null)
        {
            return accessError;
        }

        return await BuildTraceabilityResponseAsync(requestedLot, includeTimeline, includeQuality, includeMilling, includeOutbound, maxDepth, cancellationToken);
    }

    public async Task<ApiResponse> GetByLotCodeAsync(
        string lotCode,
        bool includeTimeline = true,
        bool includeQuality = true,
        bool includeMilling = true,
        bool includeOutbound = true,
        int maxDepth = 10,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lotCode))
        {
            return ApiResponse.BadRequest(message: "Mã lô hàng không được rỗng.", code: "LOT_CODE_REQUIRED");
        }

        if (maxDepth <= 0 || maxDepth > 50)
        {
            return ApiResponse.BadRequest(message: "Độ sâu truy vết không hợp lệ.", code: "TRACEABILITY_MAX_DEPTH_INVALID");
        }

        var requestedLot = await _context.PaddyLots
            .AsNoTracking()
            .Include(x => x.ProductVariant)
            .Include(x => x.RiceVariety)
            .Include(x => x.Status)
            .Include(x => x.Warehouse)
            .Include(x => x.Location)
            .FirstOrDefaultAsync(x => x.LotCode == lotCode.Trim() && !x.IsDeleted, cancellationToken);

        if (requestedLot == null)
        {
            return ApiResponse.NotFound(message: "Không tìm thấy lô cần truy vết.", code: "PADDY_LOT_NOT_FOUND");
        }

        var accessError = CheckOrganizationAccess(requestedLot);
        if (accessError != null)
        {
            return accessError;
        }

        return await BuildTraceabilityResponseAsync(requestedLot, includeTimeline, includeQuality, includeMilling, includeOutbound, maxDepth, cancellationToken);
    }

    private ApiResponse? CheckOrganizationAccess(PaddyLot requestedLot)
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null)
            return ApiResponse.Forbidden(message: "Chưa xác thực người dùng.", code: "UNAUTHORIZED");

        var roleIdsStr = user.FindFirst(Backend.Share.Constants.ClaimNames.ROLE_IDS)?.Value;
        bool isAdmin = false;
        if (!string.IsNullOrEmpty(roleIdsStr))
        {
            var roleIds = roleIdsStr.Split(',').Select(x => int.TryParse(x, out var id) ? id : 0).ToList();
            isAdmin = roleIds.Contains(Constants.CommonConstants.Role.ADMIN);
        }

        if (isAdmin) return null; // Admin has full access

        var officeIdStr = user.FindFirst(ClaimNames.OFFICE_ID)?.Value;
        if (!int.TryParse(officeIdStr, out var userOrgId))
        {
            return ApiResponse.Forbidden(message: "Tài khoản không được liên kết với tổ chức hợp lệ.", code: "ORGANIZATION_ACCESS_DENIED");
        }

        if (requestedLot.OrganizationId.HasValue && requestedLot.OrganizationId.Value != userOrgId)
        {
            return ApiResponse.Forbidden(message: "Bạn không có quyền truy cập lô của tổ chức này.", code: "ORGANIZATION_ACCESS_DENIED");
        }

        return null;
    }

    private async Task<ApiResponse> BuildTraceabilityResponseAsync(
        PaddyLot requestedLot,
        bool includeTimeline,
        bool includeQuality,
        bool includeMilling,
        bool includeOutbound,
        int maxDepth,
        CancellationToken cancellationToken)
    {
        var visitedLotIds = new HashSet<int> { requestedLot.Id };
        var visitedMillingOrderIds = new HashSet<int>();
        var visitedReceiptIds = new HashSet<int>();

        var lotMap = new Dictionary<int, PaddyLot> { [requestedLot.Id] = requestedLot };
        var lotRoleMap = new Dictionary<int, string> { [requestedLot.Id] = "REQUESTED" };
        var tracesDirectMillingSource =
            string.Equals(requestedLot.LotType, LotTypeConstants.Rice, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(requestedLot.LotType, LotTypeConstants.ByProduct, StringComparison.OrdinalIgnoreCase);

        bool isTruncated = false;
        int currentDepth = 0;
        var currentBatch = new List<int> { requestedLot.Id };

        while (currentBatch.Count > 0)
        {
            if (currentDepth >= maxDepth)
            {
                isTruncated = true;
                _logger.LogWarning("[Traceability] Dừng truy vết an toàn do vượt quá maxDepth={MaxDepth} cho lot ID {LotId}", maxDepth, requestedLot.Id);
                break;
            }
            currentDepth++;

            var nextBatch = new HashSet<int>();

            // 1. Direct references from current batch lots
            foreach (var lotId in currentBatch)
            {
                if (lotMap.TryGetValue(lotId, out var lot))
                {
                    if (lot.SourceReceiptId.HasValue)
                    {
                        visitedReceiptIds.Add(lot.SourceReceiptId.Value);
                    }
                    if (lot.SourceMillingOrderId.HasValue &&
                        (!tracesDirectMillingSource || lotId == requestedLot.Id))
                    {
                        visitedMillingOrderIds.Add(lot.SourceMillingOrderId.Value);
                    }
                }
            }

            // Only a directly requested paddy lot can fan out to every milling order
            // that consumed it. Rice/byproduct traces stay anchored to their source.
            if (!tracesDirectMillingSource)
            {
                var inputsForCurrentBatch = await _context.MillingOrderInputs
                    .AsNoTracking()
                    .Where(m => currentBatch.Contains(m.PaddyLotId) && !m.IsDeleted)
                    .Select(m => m.MillingOrderId)
                    .ToListAsync(cancellationToken);

                foreach (var moId in inputsForCurrentBatch)
                {
                    visitedMillingOrderIds.Add(moId);
                }
            }

            // 3. For all milling orders in visitedMillingOrderIds, find all input and output lots
            if (visitedMillingOrderIds.Count > 0)
            {
                var millingInputs = await _context.MillingOrderInputs
                    .AsNoTracking()
                    .Where(mi => visitedMillingOrderIds.Contains(mi.MillingOrderId) && !mi.IsDeleted)
                    .Select(mi => new { mi.MillingOrderId, mi.PaddyLotId })
                    .ToListAsync(cancellationToken);

                foreach (var mi in millingInputs)
                {
                    if (visitedLotIds.Add(mi.PaddyLotId))
                    {
                        nextBatch.Add(mi.PaddyLotId);
                        if (!lotRoleMap.ContainsKey(mi.PaddyLotId))
                        {
                            lotRoleMap[mi.PaddyLotId] = mi.PaddyLotId == requestedLot.Id ? "REQUESTED" : "MILLING_INPUT";
                        }
                    }
                }

                // Paddy traces retain their forward lineage. Rice/byproduct traces are
                // backward-only, so sibling outputs of the source order are excluded.
                if (!tracesDirectMillingSource)
                {
                    var millingOutputs = await _context.MillingOrderOutputs
                        .AsNoTracking()
                        .Where(mo => visitedMillingOrderIds.Contains(mo.MillingOrderId) && !mo.IsDeleted && mo.OutputLotId != null)
                        .Select(mo => new { mo.MillingOrderId, OutputLotId = mo.OutputLotId!.Value, mo.IsByproduct, mo.OutputType })
                        .ToListAsync(cancellationToken);

                    foreach (var mo in millingOutputs)
                    {
                        if (visitedLotIds.Add(mo.OutputLotId))
                        {
                            nextBatch.Add(mo.OutputLotId);
                            if (!lotRoleMap.ContainsKey(mo.OutputLotId))
                            {
                                if (mo.IsByproduct || mo.OutputType != LotTypeConstants.Rice)
                                {
                                    lotRoleMap[mo.OutputLotId] = "BYPRODUCT";
                                }
                                else
                                {
                                    lotRoleMap[mo.OutputLotId] = "MILLING_OUTPUT";
                                }
                            }
                        }
                    }
                }
            }

            if (nextBatch.Count == 0)
                break;

            var newlyLoadedLots = await _context.PaddyLots
                .AsNoTracking()
                .Include(x => x.ProductVariant)
                .Include(x => x.RiceVariety)
                .Include(x => x.Status)
                .Include(x => x.Warehouse)
                .Include(x => x.Location)
                .Where(x => nextBatch.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            foreach (var lot in newlyLoadedLots)
            {
                lotMap[lot.Id] = lot;

                if (!lotRoleMap.ContainsKey(lot.Id))
                {
                    if (lot.LotType == "BYPRODUCT")
                    {
                        lotRoleMap[lot.Id] = "BYPRODUCT";
                    }
                    else if (lot.SourceMillingOrderId.HasValue)
                    {
                        lotRoleMap[lot.Id] = "MILLING_OUTPUT";
                    }
                    else
                    {
                        lotRoleMap[lot.Id] = "MILLING_INPUT";
                    }
                }
            }

            currentBatch = nextBatch.ToList();
        }

        var allLotIds = lotMap.Keys.ToList();

        // 4. Batch query associated entities
        // Purchases
        var purchasesList = new List<TraceabilityPurchaseDto>();
        if (visitedReceiptIds.Count > 0)
        {
            var receipts = await _context.PaddyPurchaseReceipts
                .AsNoTracking()
                .Include(x => x.Farmer)
                .Include(x => x.RiceVariety)
                .Include(x => x.Warehouse)
                .Include(x => x.PaddyLot)
                .Where(x => visitedReceiptIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            // W14-J: Load bags của từng lô thu mua để trà về thông tin cân từng bao
            var purchaseLotIds = receipts.Where(r => r.PaddyLot != null).Select(r => r.PaddyLot!.Id).Distinct().ToList();
            var purchaseBagsByLot = purchaseLotIds.Count > 0
                ? (await _context.PaddyLotBags
                    .AsNoTracking()
                    .Where(b => purchaseLotIds.Contains(b.LotId) && b.BagKind == "Purchase" && !b.IsDeleted)
                    .ToListAsync(cancellationToken))
                    .GroupBy(b => b.LotId)
                    .ToDictionary(g => g.Key, g => g.ToList())
                : new Dictionary<int, List<PaddyLotBag>>();
            var purchaseBagSnapshotsByReceipt = receipts.ToDictionary(
                r => r.Id,
                r => ParsePurchaseBagSnapshots(r.Id, r.BagDetailsJson));
            var weighedByIds = purchaseBagsByLot.Values.SelectMany(x => x)
                .Where(x => x.WeighedBy.HasValue).Select(x => x.WeighedBy!.Value)
                .Concat(purchaseBagSnapshotsByReceipt.Values
                    .SelectMany(x => x)
                    .Where(x => x.WeighedBy.HasValue)
                    .Select(x => x.WeighedBy!.Value))
                .Distinct()
                .ToList();
            var weighedByNames = weighedByIds.Count == 0
                ? new Dictionary<int, string>()
                : await _context.Users.AsNoTracking().Where(x => weighedByIds.Contains(x.Id))
                    .ToDictionaryAsync(x => x.Id, x => (x.LastName + " " + x.FirstName).Trim(), cancellationToken);

            purchasesList = receipts.Select(r => new TraceabilityPurchaseDto
            {
                ReceiptId = r.Id,
                ReceiptCode = r.ReceiptCode,
                PaddyLotId = r.PaddyLot?.Id,
                PaddyLotCode = r.PaddyLot?.LotCode,
                ScheduleId = r.ScheduleId,
                FarmerId = r.FarmerId,
                FarmerCode = r.Farmer?.Code,
                FarmerName = r.Farmer?.Name,
                RiceVarietyId = r.RiceVarietyId,
                RiceVarietyName = r.RiceVariety?.Name,
                WarehouseId = r.WarehouseId,
                WarehouseCode = r.Warehouse?.Code,
                WarehouseName = r.Warehouse?.Name,
                ReceiptDate = r.ReceiptDate,
                ActualWeightKg = r.ActualWeightKg,
                BagCount = r.BagCount,
                QualityJson = r.QualityJson,
                InitialQuality = ParseQualityJson(r.QualityJson),
                // W14-J: Map từng bao thu mua kèm thông tin cân
                Bags = BuildPurchaseBagTrace(
                    r,
                    purchaseBagsByLot,
                    purchaseBagSnapshotsByReceipt.GetValueOrDefault(r.Id) ?? new List<CreateBagDto>(),
                    weighedByNames)
            }).ToList();
        }

        // Quality Inspections
        var inspectionsList = new List<TraceabilityInspectionDto>();
        if (includeQuality && allLotIds.Count > 0)
        {
            var inspections = await _context.QualityInspections
                .AsNoTracking()
                .Include(x => x.Inspector)
                .Include(x => x.PaddyLot)
                .Include(x => x.BagResults).ThenInclude(br => br.Bag)
                .Include(x => x.BagResults).ThenInclude(br => br.Inspector)
                .Where(x => allLotIds.Contains(x.PaddyLotId) && !x.IsDeleted)
                .OrderBy(x => x.InspectedAt)
                .ToListAsync(cancellationToken);

            inspectionsList = inspections.Select(i => new TraceabilityInspectionDto
            {
                InspectionId = i.Id,
                PaddyLotId = i.PaddyLotId,
                PaddyLotCode = i.PaddyLot?.LotCode,
                // W14-J: Map InspectionType và CompletedAt
                InspectionType = i.InspectionType,
                CompletedAt = i.CompletedAt,
                InspectedAt = i.InspectedAt,
                MoisturePercent = i.MoisturePercent,
                ImpurityPercent = i.ImpurityPercent,
                MoldLevel = i.MoldLevel,
                PestLevel = i.PestLevel,
                PackagingStatus = i.PackagingStatus,
                Handling = i.Handling,
                PassedInspection = i.PassedInspection,
                ResultName = i.PassedInspection ? "Đạt" : "Không đạt",
                InspectorId = i.InspectorId,
                InspectorName = i.Inspector != null ? $"{i.Inspector.LastName} {i.Inspector.FirstName}".Trim() : null,
                Note = i.Note,
                // W14-J: Map kết quả kiểm định từng bao
                BagResults = i.BagResults.Where(br => !br.IsDeleted).Select(br => new TraceabilityInspectionBagResultDto
                {
                    BagResultId = br.Id,
                    BagId = br.BagId,
                    BagNo = br.Bag?.BagNo ?? 0,
                    WeightKg = br.Bag?.WeightKg ?? 0,
                    MoisturePercent = br.MoisturePercent,
                    QualityResult = br.QualityResult,
                    Disposition = br.Disposition,
                    InspectorId = br.InspectorId,
                    InspectorName = br.Inspector != null ? $"{br.Inspector.LastName} {br.Inspector.FirstName}".Trim() : null,
                    InspectedAt = br.InspectedAt,
                    Handling = br.Handling,
                    Note = br.Note
                }).ToList()
            }).ToList();
        }

        // Milling Orders
        var millingList = new List<TraceabilityMillingDto>();
        if (includeMilling && visitedMillingOrderIds.Count > 0)
        {
            var millingOrders = await _context.MillingOrders
                .AsNoTracking()
                .Include(x => x.Status)
                .Include(x => x.Warehouse)
                // W14-J: Include Operator để lấy tên người vận hành
                .Include(x => x.Operator)
                .Include(x => x.MillingOrderInputs).ThenInclude(i => i.PaddyLot).ThenInclude(l => l.ProductVariant)
                .Include(x => x.MillingOrderInputs).ThenInclude(i => i.Location)
                .Include(x => x.MillingOrderOutputs).ThenInclude(o => o.ProductVariant)
                .Include(x => x.MillingOrderOutputs).ThenInclude(o => o.OutputLot)
                .Include(x => x.MillingOrderOutputs).ThenInclude(o => o.Location)
                .Where(x => visitedMillingOrderIds.Contains(x.Id) && !x.IsDeleted)
                .ToListAsync(cancellationToken);

            millingList = millingOrders.Select(m => new TraceabilityMillingDto
            {
                MillingOrderId = m.Id,
                MillingCode = m.MillingCode,
                StatusId = m.StatusId,
                StatusName = m.Status?.Name,
                StatusCode = m.Status?.Code,
                WarehouseId = m.WarehouseId,
                WarehouseCode = m.Warehouse?.Code,
                WarehouseName = m.Warehouse?.Name,
                SalesOrderId = m.SalesOrderId,
                YieldRateUsed = m.YieldRateUsed,
                ComputedPaddyKg = m.ComputedPaddyKg,
                TotalRiceOutputKg = m.TotalRiceOutputKg,
                ByproductKg = m.ByproductKg,
                LossKg = m.LossKg,
                StartedAt = m.StartedAt,
                CompletedAt = m.CompletedAt,
                // W14-J: Map machine/operator/actual yield
                MachineRef = m.MachineRef,
                OperatorId = m.OperatorId,
                OperatorName = m.Operator != null ? $"{m.Operator.LastName} {m.Operator.FirstName}".Trim() : null,
                ActualPaddyInputKg = m.ActualPaddyInputKg,
                ActualYieldRate = m.ActualYieldRate,
                Inputs = m.MillingOrderInputs.Where(i => !i.IsDeleted).Select(i => new TraceabilityMillingInputDto
                {
                    MillingOrderInputId = i.Id,
                    PaddyLotId = i.PaddyLotId,
                    PaddyLotCode = i.PaddyLot?.LotCode,
                    LotType = i.PaddyLot?.LotType,
                    ProductVariantId = i.PaddyLot?.ProductVariantId ?? 0,
                    Sku = i.PaddyLot?.ProductVariant?.SKU,
                    LocationId = i.LocationId,
                    LocationCode = i.Location != null ? (i.Location.SlotCode ?? i.Location.QrCode) : null,
                    ReservedWeightKg = i.ReservedWeightKg,
                    ConsumedWeightKg = i.ConsumedWeightKg,
                    Note = i.Note
                }).ToList(),
                Outputs = m.MillingOrderOutputs.Where(o => !o.IsDeleted).Select(o => new TraceabilityMillingOutputDto
                {
                    MillingOrderOutputId = o.Id,
                    OutputLotId = o.OutputLotId,
                    OutputLotCode = o.OutputLot?.LotCode,
                    ProductVariantId = o.ProductVariantId,
                    Sku = o.ProductVariant?.SKU,
                    ProductVariantName = o.ProductVariant?.Name,
                    OutputType = o.OutputType,
                    OutputWeightKg = o.OutputWeightKg,
                    BagCount = o.BagCount,
                    IsByproduct = o.IsByproduct,
                    LocationId = o.LocationId,
                    LocationCode = o.Location != null ? (o.Location.SlotCode ?? o.Location.QrCode) : null
                }).ToList()
            }).ToList();
        }

        // Outbound Sales
        var outboundList = new List<TraceabilityOutboundDto>();
        if (includeOutbound && allLotIds.Count > 0)
        {
            var rawAllocations = await _context.OutboundOrderItemAllocations
                .AsNoTracking()
                .Include(a => a.PaddyLot)
                .Include(a => a.Location)
                .Include(a => a.OutboundOrderItem).ThenInclude(oi => oi.ProductVariant)
                .Include(a => a.OutboundOrderItem).ThenInclude(oi => oi.OutboundOrder).ThenInclude(o => o.Warehouse)
                .Include(a => a.OutboundOrderItem).ThenInclude(oi => oi.OutboundOrder).ThenInclude(o => o.OutboundOrderStatus)
                .Include(a => a.OutboundOrderItem).ThenInclude(oi => oi.OutboundOrder).ThenInclude(o => o.SalesOrder).ThenInclude(s => s.Customer)
                .Include(a => a.OutboundOrderItem).ThenInclude(oi => oi.OutboundOrder).ThenInclude(o => o.SalesOrder).ThenInclude(s => s.Status)
                .Where(a => a.PaddyLotId.HasValue && allLotIds.Contains(a.PaddyLotId.Value) && !a.IsDeleted
                         && !a.OutboundOrderItem.IsDeleted
                         && !a.OutboundOrderItem.OutboundOrder.IsDeleted
                         && !a.OutboundOrderItem.OutboundOrder.SalesOrder.IsDeleted)
                .ToListAsync(cancellationToken);

            var groupedOutbounds = rawAllocations
                .GroupBy(a => a.OutboundOrderItem.OutboundOrder)
                .ToList();

            foreach (var group in groupedOutbounds)
            {
                var ob = group.Key;
                var so = ob.SalesOrder;

                outboundList.Add(new TraceabilityOutboundDto
                {
                    OutboundOrderId = ob.Id,
                    OutboundStatusId = ob.OutboundOrderStatusId,
                    OutboundStatusName = ob.OutboundOrderStatus?.Name,
                    OutboundStatusCode = ob.OutboundOrderStatus?.Code,
                    CompletedDate = ob.CompletedDate,
                    WarehouseId = ob.WarehouseId,
                    WarehouseCode = ob.Warehouse?.Code,
                    WarehouseName = ob.Warehouse?.Name,
                    SalesOrderId = ob.SalesOrderId,
                    SalesOrderCode = so?.SOCode,
                    SalesOrderStatusId = so?.StatusId,
                    SalesOrderStatusName = so?.Status?.Name,
                    SalesOrderStatusCode = so?.Status?.Code,
                    Channel = so?.Channel,
                    SalesOrderDate = so?.OrderDate,
                    CustomerId = so?.CustomerId,
                    CustomerCode = so?.Customer?.Code,
                    CustomerName = so?.Customer?.Name,
                    Allocations = group.Select(a => new TraceabilityOutboundAllocationDto
                    {
                        AllocationId = a.Id,
                        OutboundOrderItemId = a.OutboundOrderItemId,
                        PaddyLotId = a.PaddyLotId,
                        PaddyLotCode = a.PaddyLot?.LotCode,
                        ProductVariantId = a.OutboundOrderItem.ProductVariantId,
                        Sku = a.OutboundOrderItem.ProductVariant?.SKU,
                        ProductVariantName = a.OutboundOrderItem.ProductVariant?.Name,
                        LocationId = a.LocationId,
                        LocationCode = a.Location != null ? (a.Location.SlotCode ?? a.Location.QrCode) : null,
                        QuantityAllocatedKg = a.QuantityAllocated,
                        QuantityPickedKg = a.QuantityPicked
                    }).ToList()
                });
            }
        }

        // Customer feedbacks connected to the traced outbound graph.
        var feedbacksList = new List<TraceabilityCustomerFeedbackDto>();
        var outboundIds = outboundList.Select(x => x.OutboundOrderId).Distinct().ToList();
        var tracedOutboundItemIds = outboundList
            .SelectMany(x => x.Allocations)
            .Select(x => x.OutboundOrderItemId)
            .Distinct()
            .ToHashSet();
        if (outboundIds.Count > 0)
        {
            var feedbacks = await _context.CustomerFeedbacks
                .AsNoTracking()
                .Include(f => f.PaddyLotBagAllocation).ThenInclude(a => a.Bag).ThenInclude(b => b.Lot)
                .Include(f => f.CustomerReturnOrder)
                .Where(f => outboundIds.Contains(f.OutboundOrderId) && !f.IsDeleted)
                .OrderBy(f => f.CreatedDate)
                .ToListAsync(cancellationToken);

            feedbacksList = feedbacks
                .Where(f =>
                    (f.PaddyLotBagAllocationId.HasValue
                        && f.PaddyLotBagAllocation?.Bag != null
                        && allLotIds.Contains(f.PaddyLotBagAllocation.Bag.LotId))
                    || (f.OutboundOrderItemId.HasValue
                        && tracedOutboundItemIds.Contains(f.OutboundOrderItemId.Value))
                    || (!f.PaddyLotBagAllocationId.HasValue && !f.OutboundOrderItemId.HasValue))
                .Select(f => new TraceabilityCustomerFeedbackDto
                {
                    FeedbackId = f.Id,
                    SalesOrderId = f.SalesOrderId,
                    OutboundOrderId = f.OutboundOrderId,
                    OutboundOrderItemId = f.OutboundOrderItemId,
                    PaddyLotBagAllocationId = f.PaddyLotBagAllocationId,
                    BagId = f.PaddyLotBagAllocation?.BagId,
                    BagNo = f.PaddyLotBagAllocation?.Bag?.BagNo,
                    PaddyLotId = f.PaddyLotBagAllocation?.Bag?.LotId,
                    PaddyLotCode = f.PaddyLotBagAllocation?.Bag?.Lot?.LotCode,
                    FeedbackType = f.FeedbackType,
                    Severity = f.Severity,
                    Description = f.Description,
                    ResolutionStatus = f.ResolutionStatus,
                    ResolutionNote = f.ResolutionNote,
                    CreatedDate = f.CreatedDate,
                    ResolvedAt = f.ResolvedAt,
                    CustomerReturnOrderId = f.CustomerReturnOrder?.Id,
                    CustomerReturnOrderCode = f.CustomerReturnOrder?.ReturnCode
                })
                .ToList();
        }

        // Customer returns preserve the original outbound allocation and lot lineage.
        var returnsList = new List<TraceabilityCustomerReturnDto>();
        var refundTransactions = new List<DebtTransaction>();
        if (outboundIds.Count > 0 && allLotIds.Count > 0)
        {
            var returns = await _context.CustomerReturnOrders
                .AsNoTracking()
                .Include(r => r.CustomerReturnOrderStatus)
                .Include(r => r.OutboundOrder).ThenInclude(o => o!.SalesOrder)
                .Include(r => r.Items.Where(i => !i.IsDeleted)).ThenInclude(i => i.ProductVariant)
                .Include(r => r.Items.Where(i => !i.IsDeleted))
                    .ThenInclude(i => i.Allocations.Where(a => !a.IsDeleted)).ThenInclude(a => a.PaddyLot)
                .Where(r => !r.IsDeleted
                    && r.OutboundOrderId.HasValue
                    && outboundIds.Contains(r.OutboundOrderId.Value)
                    && r.Items.Any(i => !i.IsDeleted
                        && i.Allocations.Any(a => !a.IsDeleted && allLotIds.Contains(a.PaddyLotId))))
                .AsSplitQuery()
                .OrderBy(r => r.CreatedDate)
                .ToListAsync(cancellationToken);

            var returnOrderIds = returns.Select(r => r.Id).ToList();
            refundTransactions = await _context.DebtTransactions
                .AsNoTracking()
                .Where(x => !x.IsDeleted
                    && x.TransactionType == LookupCodes.DebtTransactionType.Payment
                    && x.RefType == InventoryReferenceTypeConstants.CustomerReturnOrder
                    && x.RefId.HasValue
                    && returnOrderIds.Contains(x.RefId.Value))
                .OrderBy(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .ToListAsync(cancellationToken);
            var refundsByReturnId = refundTransactions
                .GroupBy(x => x.RefId!.Value)
                .ToDictionary(x => x.Key, x => x.ToList());

            returnsList = returns.Select(r =>
            {
                refundsByReturnId.TryGetValue(r.Id, out var actualRefunds);
                var refundedAmount = actualRefunds?.Sum(x => x.Amount) ?? 0m;
                var latestRefundAt = actualRefunds?.Max(x => (DateTime?)x.TransactionDate);

                return new TraceabilityCustomerReturnDto
                {
                    CustomerReturnOrderId = r.Id,
                    ReturnCode = r.ReturnCode,
                    OutboundOrderId = r.OutboundOrderId ?? 0,
                    SalesOrderId = r.OutboundOrder?.SalesOrderId ?? 0,
                    CustomerFeedbackId = r.CustomerFeedbackId,
                    CustomerId = r.CustomerId,
                    WarehouseId = r.WarehouseId,
                    StatusCode = r.CustomerReturnOrderStatus?.Code ?? string.Empty,
                    ReturnReason = r.ReturnReason,
                    CreatedDate = r.CreatedDate,
                    ReceivedAt = r.ReceivedAt,
                    InspectedAt = r.InspectedAt,
                    ConfirmedAt = r.ConfirmedAt,
                    ApprovedCreditAmount = r.ApprovedCreditAmount,
                    DebtReductionAmount = r.DebtReductionAmount,
                    RefundPendingAmount = r.RefundPendingAmount,
                    RefundedAmount = refundedAmount,
                    RefundStatus = r.RefundStatus,
                    RefundedAt = latestRefundAt,
                    Items = r.Items.Where(i => !i.IsDeleted)
                        .Select(i => new TraceabilityCustomerReturnItemDto
                        {
                            CustomerReturnOrderItemId = i.Id,
                            ProductVariantId = i.ProductVariantId,
                            ProductVariantName = i.ProductVariant?.Name,
                            Sku = i.ProductVariant?.SKU,
                            Allocations = i.Allocations
                                .Where(a => !a.IsDeleted && allLotIds.Contains(a.PaddyLotId))
                                .Select(a => new TraceabilityCustomerReturnAllocationDto
                                {
                                    ReturnAllocationId = a.Id,
                                    OutboundOrderItemAllocationId = a.OutboundOrderItemAllocationId,
                                    PaddyLotId = a.PaddyLotId,
                                    PaddyLotCode = a.PaddyLot?.LotCode,
                                    ProductVariantId = a.ProductVariantId,
                                    QuantityReturned = a.QuantityReturned,
                                    QuantityReceived = a.QuantityReceived,
                                    QuantityGood = a.QuantityGood,
                                    QuantityDamaged = a.QuantityDamaged,
                                    QuantityRejected = a.QuantityRejected,
                                    Disposition = a.Disposition,
                                    RestockLocationId = a.RestockLocationId,
                                    QuarantineLocationId = a.QuarantineLocationId,
                                    RejectedLocationId = a.RejectedLocationId,
                                    CreditAmount = a.CreditAmount
                                }).ToList()
                        })
                        .Where(i => i.Allocations.Count > 0)
                        .ToList()
                };
            }).ToList();
        }

        // Batch query active inventories for location mapping
        var activeInvs = await _context.Inventories
            .AsNoTracking()
            .Include(i => i.Location)
            .Where(i => allLotIds.Contains(i.PaddyLotId!.Value) && !i.IsDeleted && i.QuantityOnHand > 0)
            .ToListAsync(cancellationToken);

        var invGroups = activeInvs
            .GroupBy(i => i.PaddyLotId!.Value)
            .ToDictionary(g => g.Key, g => g.ToList());

        // Map Related Lots
        var relatedLotsList = lotMap.Values.Select(l => 
        {
            var locationId = l.LocationId;
            var locationCode = l.Location != null ? (l.Location.SlotCode ?? l.Location.QrCode) : null;
            if (invGroups.TryGetValue(l.Id, out var invs) && invs.Any())
            {
                locationId = invs.First().LocationId;
                locationCode = string.Join(", ", invs.Select(x => x.Location?.SlotCode ?? x.Location?.QrCode).Distinct().Where(c => c != null));
            }

            return new TraceabilityLotDto
            {
                Id = l.Id,
                LotCode = l.LotCode,
                LotType = l.LotType,
                RelationRole = lotRoleMap.GetValueOrDefault(l.Id, "MILLING_OUTPUT"),
                ProductVariantId = l.ProductVariantId,
                Sku = l.ProductVariant?.SKU,
                ProductVariantName = l.ProductVariant?.Name,
                RiceVarietyId = l.RiceVarietyId,
                RiceVarietyName = l.RiceVariety?.Name,
                StatusId = l.StatusId,
                StatusName = l.Status?.Name,
                StatusCode = l.Status?.Code,
                IsSellable = l.Status?.IsSellable ?? false,
                IsQuarantined = l.Status?.Code == LotStatusCodeConstants.Quarantine,
                WarehouseId = l.WarehouseId,
                WarehouseCode = l.Warehouse?.Code,
                WarehouseName = l.Warehouse?.Name,
                LocationId = locationId,
                LocationCode = locationCode,
                InboundDate = l.InboundDate,
                InitialWeightKg = l.InitialWeightKg,
                RemainingWeightKg = l.RemainingWeightKg,
                QualityStatus = l.QualityStatus,
                SourceReceiptId = l.SourceReceiptId,
                SourceMillingOrderId = l.SourceMillingOrderId
            };
        }).ToList();

        // 5. Build Timeline
        var timeline = new List<TraceabilityEventDto>();
        if (includeTimeline)
        {
            // Procurement events
            foreach (var p in purchasesList)
            {
                timeline.Add(new TraceabilityEventDto
                {
                    EventAt = p.ReceiptDate,
                    EventType = "PROCUREMENT",
                    ReferenceType = "PADDY_PURCHASE_RECEIPT",
                    ReferenceId = p.ReceiptId,
                    ReferenceCode = p.ReceiptCode,
                    PaddyLotIds = p.PaddyLotId.HasValue ? new List<int> { p.PaddyLotId.Value } : new List<int>(),
                    Title = "Thu mua lúa",
                    Description = $"Nhận {p.ActualWeightKg:0.##} kg lúa từ nông dân {p.FarmerName ?? "KĐX"} (Phiếu: {p.ReceiptCode})",
                    QuantityKg = p.ActualWeightKg,
                    Status = "Hoàn thành",
                    Sequence = 1
                });
            }

            // Quality Inspection events
            foreach (var i in inspectionsList)
            {
                timeline.Add(new TraceabilityEventDto
                {
                    EventAt = i.InspectedAt,
                    EventType = "QUALITY_INSPECTION",
                    ReferenceType = "QUALITY_INSPECTION",
                    ReferenceId = i.InspectionId,
                    ReferenceCode = $"INSP-{i.InspectionId:D5}",
                    PaddyLotIds = new List<int> { i.PaddyLotId },
                    Title = "Kiểm định chất lượng",
                    Description = $"Kiểm định lô {i.PaddyLotCode}: {(i.PassedInspection ? "Đạt" : "Không đạt")} (Độ ẩm: {i.MoisturePercent:0.#}%, Tạp chất: {i.ImpurityPercent:0.#}%)",
                    QuantityKg = null,
                    Status = i.PassedInspection ? "Đạt" : "Không đạt",
                    Sequence = 2
                });
            }

            // Milling Order events
            foreach (var m in millingList)
            {
                var inputLotIds = m.Inputs.Select(x => x.PaddyLotId).Distinct().ToList();
                var outputLotIds = m.Outputs.Where(x => x.OutputLotId.HasValue).Select(x => x.OutputLotId!.Value).Distinct().ToList();

                if (m.StartedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = m.StartedAt.Value,
                        EventType = "MILLING_STARTED",
                        ReferenceType = "MILLING_ORDER",
                        ReferenceId = m.MillingOrderId,
                        ReferenceCode = m.MillingCode,
                        PaddyLotIds = inputLotIds,
                        Title = "Bắt đầu xay xát",
                        Description = $"Bắt đầu xay xát lệnh {m.MillingCode} với {inputLotIds.Count} lô lúa đầu vào",
                        QuantityKg = m.ComputedPaddyKg,
                        Status = m.StatusName,
                        Sequence = 3
                    });
                }

                if (m.CompletedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = m.CompletedAt.Value,
                        EventType = "MILLING_COMPLETED",
                        ReferenceType = "MILLING_ORDER",
                        ReferenceId = m.MillingOrderId,
                        ReferenceCode = m.MillingCode,
                        PaddyLotIds = outputLotIds,
                        Title = "Hoàn thành xay xát",
                        Description = $"Hoàn thành xay xát lệnh {m.MillingCode}, sản xuất {m.TotalRiceOutputKg:0.##} kg gạo thành phẩm",
                        QuantityKg = m.TotalRiceOutputKg,
                        Status = m.StatusName,
                        Sequence = 4
                    });
                }

                if (!m.StartedAt.HasValue && !m.CompletedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = DateTimeHelper.VietnamNow(),
                        EventType = "MILLING",
                        ReferenceType = "MILLING_ORDER",
                        ReferenceId = m.MillingOrderId,
                        ReferenceCode = m.MillingCode,
                        PaddyLotIds = inputLotIds.Concat(outputLotIds).Distinct().ToList(),
                        Title = "Lệnh xay xát",
                        Description = $"Lệnh xay xát {m.MillingCode}",
                        QuantityKg = m.TotalRiceOutputKg,
                        Status = m.StatusName,
                        Sequence = 3
                    });
                }
            }

            // Outbound events
            foreach (var ob in outboundList)
            {
                var lotIds = ob.Allocations.Where(a => a.PaddyLotId.HasValue).Select(a => a.PaddyLotId!.Value).Distinct().ToList();
                var totalKg = ob.Allocations.Sum(a => a.QuantityPickedKg > 0 ? a.QuantityPickedKg : a.QuantityAllocatedKg);

                if (ob.CompletedDate.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = ob.CompletedDate.Value,
                        EventType = "OUTBOUND_COMPLETED",
                        ReferenceType = "OUTBOUND_ORDER",
                        ReferenceId = ob.OutboundOrderId,
                        ReferenceCode = $"OB-{ob.OutboundOrderId:D5}",
                        PaddyLotIds = lotIds,
                        Title = "Xuất kho bán hàng thành công",
                        Description = $"Xuất kho {totalKg:0.##} kg cho đơn bán {ob.SalesOrderCode ?? "N/A"} - Khách hàng: {ob.CustomerName ?? "Lẻ"}",
                        QuantityKg = totalKg,
                        Status = ob.OutboundStatusName,
                        Sequence = 6
                    });
                }
                else
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = ob.SalesOrderDate ?? DateTimeHelper.VietnamNow(),
                        EventType = "OUTBOUND_ALLOCATED",
                        ReferenceType = "OUTBOUND_ORDER",
                        ReferenceId = ob.OutboundOrderId,
                        ReferenceCode = $"OB-{ob.OutboundOrderId:D5}",
                        PaddyLotIds = lotIds,
                        Title = "Phân bổ xuất kho bán hàng",
                        Description = $"Phân bổ xuất kho cho đơn bán {ob.SalesOrderCode ?? "N/A"} - Khách hàng: {ob.CustomerName ?? "Lẻ"}",
                        QuantityKg = totalKg,
                        Status = ob.OutboundStatusName,
                        Sequence = 5
                    });
                }
            }

            foreach (var feedback in feedbacksList)
            {
                var lotIds = feedback.PaddyLotId.HasValue
                    ? new List<int> { feedback.PaddyLotId.Value }
                    : outboundList
                        .Where(o => o.OutboundOrderId == feedback.OutboundOrderId)
                        .SelectMany(o => o.Allocations)
                        .Where(a => !feedback.OutboundOrderItemId.HasValue
                            || a.OutboundOrderItemId == feedback.OutboundOrderItemId.Value)
                        .Where(a => a.PaddyLotId.HasValue)
                        .Select(a => a.PaddyLotId!.Value)
                        .Distinct()
                        .ToList();

                timeline.Add(new TraceabilityEventDto
                {
                    EventAt = feedback.CreatedDate,
                    EventType = "CUSTOMER_FEEDBACK_CREATED",
                    ReferenceType = "CUSTOMER_FEEDBACK",
                    ReferenceId = feedback.FeedbackId,
                    ReferenceCode = $"FB-{feedback.FeedbackId:D5}",
                    PaddyLotIds = lotIds,
                    Title = "Ghi nhận phản hồi khách hàng",
                    Description = $"{feedback.FeedbackType} · {feedback.Description}",
                    Status = feedback.ResolutionStatus,
                    Sequence = 7
                });

                if (feedback.ResolvedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = feedback.ResolvedAt.Value,
                        EventType = "CUSTOMER_FEEDBACK_RESOLVED",
                        ReferenceType = "CUSTOMER_FEEDBACK",
                        ReferenceId = feedback.FeedbackId,
                        ReferenceCode = $"FB-{feedback.FeedbackId:D5}",
                        PaddyLotIds = lotIds,
                        Title = "Hoàn tất xử lý phản hồi",
                        Description = feedback.ResolutionNote ?? feedback.Description,
                        Status = feedback.ResolutionStatus,
                        Sequence = 8
                    });
                }
            }

            foreach (var customerReturn in returnsList)
            {
                var allocations = customerReturn.Items.SelectMany(i => i.Allocations).ToList();
                var lotIds = allocations.Select(a => a.PaddyLotId).Distinct().ToList();
                var returnedKg = allocations.Sum(a => a.QuantityReturned);

                timeline.Add(new TraceabilityEventDto
                {
                    EventAt = customerReturn.CreatedDate,
                    EventType = "CUSTOMER_RETURN_CREATED",
                    ReferenceType = "CUSTOMER_RETURN_ORDER",
                    ReferenceId = customerReturn.CustomerReturnOrderId,
                    ReferenceCode = customerReturn.ReturnCode,
                    PaddyLotIds = lotIds,
                    Title = "Tạo phiếu trả hàng khách",
                    Description = customerReturn.ReturnReason ?? $"Khách trả {returnedKg:0.##} kg hàng",
                    QuantityKg = returnedKg,
                    Status = customerReturn.StatusCode,
                    Sequence = 9
                });

                if (customerReturn.ReceivedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = customerReturn.ReceivedAt.Value,
                        EventType = "CUSTOMER_RETURN_RECEIVED",
                        ReferenceType = "CUSTOMER_RETURN_ORDER",
                        ReferenceId = customerReturn.CustomerReturnOrderId,
                        ReferenceCode = customerReturn.ReturnCode,
                        PaddyLotIds = lotIds,
                        Title = "Tiếp nhận hàng khách trả",
                        Description = $"Đã nhận {allocations.Sum(a => a.QuantityReceived):0.##} kg hàng trả",
                        QuantityKg = allocations.Sum(a => a.QuantityReceived),
                        Status = customerReturn.StatusCode,
                        Sequence = 10
                    });
                }

                if (customerReturn.InspectedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = customerReturn.InspectedAt.Value,
                        EventType = "CUSTOMER_RETURN_INSPECTED",
                        ReferenceType = "CUSTOMER_RETURN_ORDER",
                        ReferenceId = customerReturn.CustomerReturnOrderId,
                        ReferenceCode = customerReturn.ReturnCode,
                        PaddyLotIds = lotIds,
                        Title = "Kiểm định hàng khách trả",
                        Description = $"Đạt {allocations.Sum(a => a.QuantityGood):0.##} kg · Cách ly {allocations.Sum(a => a.QuantityDamaged):0.##} kg · Từ chối {allocations.Sum(a => a.QuantityRejected):0.##} kg",
                        QuantityKg = allocations.Sum(a => a.QuantityReceived),
                        Status = customerReturn.StatusCode,
                        Sequence = 11
                    });
                }

                if (customerReturn.ConfirmedAt.HasValue)
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = customerReturn.ConfirmedAt.Value,
                        EventType = "CUSTOMER_RETURN_CONFIRMED",
                        ReferenceType = "CUSTOMER_RETURN_ORDER",
                        ReferenceId = customerReturn.CustomerReturnOrderId,
                        ReferenceCode = customerReturn.ReturnCode,
                        PaddyLotIds = lotIds,
                        Title = "Xác nhận xử lý hàng trả",
                        Description = $"Nhập lại {allocations.Sum(a => a.QuantityGood):0.##} kg · Cách ly {allocations.Sum(a => a.QuantityDamaged):0.##} kg · Từ chối {allocations.Sum(a => a.QuantityRejected):0.##} kg",
                        QuantityKg = returnedKg,
                        Status = customerReturn.StatusCode,
                        Sequence = 12
                    });
                }

                foreach (var refund in refundTransactions.Where(x =>
                    x.RefId == customerReturn.CustomerReturnOrderId))
                {
                    timeline.Add(new TraceabilityEventDto
                    {
                        EventAt = refund.TransactionDate,
                        EventType = "CUSTOMER_RETURN_REFUND",
                        ReferenceType = "CUSTOMER_RETURN_ORDER",
                        ReferenceId = customerReturn.CustomerReturnOrderId,
                        ReferenceCode = customerReturn.ReturnCode,
                        PaddyLotIds = lotIds,
                        Title = "Hoàn tiền khách hàng",
                        Description = $"Đã hoàn {refund.Amount:0.##} đồng",
                        Status = customerReturn.RefundStatus,
                        Sequence = 13
                    });
                }
            }

            timeline = timeline
                .OrderBy(e => e.EventAt)
                .ThenBy(e => e.Sequence)
                .ThenBy(e => e.ReferenceId)
                .ToList();
        }

        // 6. Build Summary
        var summary = new TraceabilitySummaryDto
        {
            RelatedLotCount = relatedLotsList.Count,
            PurchaseReceiptCount = purchasesList.Select(x => x.ReceiptId).Distinct().Count(),
            InspectionCount = inspectionsList.Select(x => x.InspectionId).Distinct().Count(),
            MillingOrderCount = millingList.Select(x => x.MillingOrderId).Distinct().Count(),
            OutboundOrderCount = outboundList.Select(x => x.OutboundOrderId).Distinct().Count(),
            FeedbackCount = feedbacksList.Select(x => x.FeedbackId).Distinct().Count(),
            CustomerReturnCount = returnsList.Select(x => x.CustomerReturnOrderId).Distinct().Count(),
            PurchasedWeightKg = purchasesList.Sum(x => x.ActualWeightKg),
            MillingInputWeightKg = millingList.SelectMany(m => m.Inputs).Sum(i => i.ConsumedWeightKg),
            MillingRiceOutputWeightKg = millingList.SelectMany(m => m.Outputs).Where(o => !o.IsByproduct && o.OutputType == "RICE").Sum(o => o.OutputWeightKg),
            MillingByproductWeightKg = millingList.SelectMany(m => m.Outputs).Where(o => o.IsByproduct || o.OutputType != "RICE").Sum(o => o.OutputWeightKg),
            MillingLossWeightKg = millingList.Sum(m => m.LossKg ?? 0m),
            AllocatedOutboundWeightKg = outboundList.SelectMany(o => o.Allocations).Sum(a => a.QuantityAllocatedKg),
            DispatchedWeightKg = outboundList.SelectMany(o => o.Allocations).Sum(a => a.QuantityPickedKg),
            ReturnedWeightKg = returnsList.SelectMany(r => r.Items).SelectMany(i => i.Allocations).Sum(a => a.QuantityReturned),
            RestockedReturnWeightKg = returnsList.SelectMany(r => r.Items).SelectMany(i => i.Allocations).Sum(a => a.QuantityGood),
            QuarantinedReturnWeightKg = returnsList.SelectMany(r => r.Items).SelectMany(i => i.Allocations).Sum(a => a.QuantityDamaged),
            RejectedReturnWeightKg = returnsList.SelectMany(r => r.Items).SelectMany(i => i.Allocations).Sum(a => a.QuantityRejected),
            RefundAmount = returnsList.Sum(r => r.RefundedAmount),
            CurrentRemainingWeightKg = relatedLotsList.Sum(l => l.RemainingWeightKg)
        };

        var result = new PaddyLotTraceabilityDto
        {
            RequestedLotId = requestedLot.Id,
            RequestedLotCode = requestedLot.LotCode,
            IsTruncated = isTruncated,
            RelatedLots = relatedLotsList,
            Purchases = purchasesList,
            QualityInspections = inspectionsList,
            MillingOrders = millingList,
            OutboundSales = outboundList,
            CustomerFeedbacks = feedbacksList,
            CustomerReturns = returnsList,
            Timeline = timeline,
            Summary = summary
        };

        return ApiResponse.Success(result, "Truy vết lô thành công.");
    }

    private static object? ParseQualityJson(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;
        try
        {
            using var doc = JsonDocument.Parse(rawJson);
            return JsonSerializer.Deserialize<object>(rawJson);
        }
        catch
        {
            return null;
        }
    }

    private List<CreateBagDto> ParsePurchaseBagSnapshots(int receiptId, string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return new List<CreateBagDto>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<CreateBagDto>>(rawJson, PurchaseBagJsonOptions)
                ?? new List<CreateBagDto>();
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            _logger.LogWarning(
                ex,
                "[Traceability] Invalid BagDetailsJson for purchase receipt ID {ReceiptId}; using current bag data as legacy fallback.",
                receiptId);
            return new List<CreateBagDto>();
        }
    }

    private static List<TraceabilityPurchaseBagDto> BuildPurchaseBagTrace(
        PaddyPurchaseReceipt receipt,
        IReadOnlyDictionary<int, List<PaddyLotBag>> purchaseBagsByLot,
        IReadOnlyCollection<CreateBagDto> snapshots,
        IReadOnlyDictionary<int, string> weighedByNames)
    {
        var currentBags = receipt.PaddyLot != null &&
            purchaseBagsByLot.TryGetValue(receipt.PaddyLot.Id, out var lotBags)
                ? lotBags
                : new List<PaddyLotBag>();

        if (snapshots.Count == 0)
        {
            return currentBags.Select(b => new TraceabilityPurchaseBagDto
            {
                BagId = b.Id,
                BagNo = b.BagNo,
                WeightKg = b.WeightKg,
                ScaleDeviceRef = b.ScaleDeviceRef,
                WeightCaptureMethod = b.WeightCaptureMethod,
                WeighedAt = b.WeighedAt,
                WeighedBy = b.WeighedBy,
                WeighedByName = GetWeighedByName(b.WeighedBy, weighedByNames)
            }).ToList();
        }

        var currentBagsByNo = currentBags
            .GroupBy(b => b.BagNo)
            .ToDictionary(g => g.Key, g => g.First());

        return snapshots.Select(snapshot =>
        {
            currentBagsByNo.TryGetValue(snapshot.BagNo, out var currentBag);
            return new TraceabilityPurchaseBagDto
            {
                BagId = currentBag?.Id ?? 0,
                BagNo = snapshot.BagNo,
                WeightKg = snapshot.WeightKg,
                ScaleDeviceRef = snapshot.ScaleDeviceRef,
                WeightCaptureMethod = snapshot.WeightCaptureMethod,
                WeighedAt = snapshot.WeighedAt,
                WeighedBy = snapshot.WeighedBy,
                WeighedByName = GetWeighedByName(snapshot.WeighedBy, weighedByNames)
            };
        }).ToList();
    }

    private static string? GetWeighedByName(
        int? weighedBy,
        IReadOnlyDictionary<int, string> weighedByNames)
    {
        return weighedBy.HasValue && weighedByNames.TryGetValue(weighedBy.Value, out var name)
            ? name
            : null;
    }
}
