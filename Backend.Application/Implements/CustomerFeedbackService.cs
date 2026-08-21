using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DTOs.CustomerFeedbacks;
using Backend.Application.Interfaces;
using Backend.Application.Constants;
using Backend.Domain.Abstractions;
using Backend.Domain.Enums;
using Backend.Domain.Entities;
using Backend.Share.Constants;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Backend.Share.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Backend.Domain.DTParameters;

namespace Backend.Application.Implements;

public class CustomerFeedbackService : ICustomerFeedbackService
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IPaddyLotTraceabilityService _traceabilityService;

    public CustomerFeedbackService(
        IApplicationDbContext context,
        IHttpContextAccessor httpContextAccessor,
        IPaddyLotTraceabilityService traceabilityService)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _traceabilityService = traceabilityService;
    }

    private int GetCurrentUserId()
        => _httpContextAccessor.HttpContext?.GetCurrentUserId() ?? 1;

    public async Task<ApiResponse> CreateAsync(CreateCustomerFeedbackDto dto, CancellationToken cancellationToken = default)
    {
        if (!CustomerFeedbackType.IsValid(dto.FeedbackType))
            return ApiResponse.BadRequest(message: "Loại feedback không hợp lệ.");

        var outbound = await _context.OutboundOrders
            .Include(o => o.OutboundOrderStatus)
            .Include(o => o.SalesOrder)
            .FirstOrDefaultAsync(o => o.Id == dto.OutboundOrderId && !o.IsDeleted, cancellationToken);

        if (outbound == null)
            return ApiResponse.NotFound(message: "Không tìm thấy phiếu xuất kho.");

        if (outbound.SalesOrderId != dto.SalesOrderId)
            return ApiResponse.BadRequest(message: "Phiếu xuất kho không thuộc đơn hàng này.");

        if (outbound.OutboundOrderStatus == null)
            return ApiResponse.UnprocessableEntity(message: "Phiếu xuất chưa có trạng thái hợp lệ.");

        // Chỉ cho phép feedback khi đã giao hàng xong
        if (outbound.OutboundOrderStatus.Code != OutboundOrderStatusNames.Completed)
        {
            return ApiResponse.UnprocessableEntity(message: "Chỉ được tạo khiếu nại sau khi giao hàng.");
        }

        if (dto.OutboundOrderItemId.HasValue)
        {
            var item = await _context.OutboundOrderItems
                .FirstOrDefaultAsync(x => x.Id == dto.OutboundOrderItemId.Value && x.OutboundOrderId == dto.OutboundOrderId && !x.IsDeleted, cancellationToken);
            if (item == null)
                return ApiResponse.BadRequest(message: "Chi tiết xuất kho không hợp lệ.");
                
            if (dto.ProductVariantId.HasValue && item.ProductVariantId != dto.ProductVariantId)
                return ApiResponse.BadRequest(message: "Sản phẩm không khớp với chi tiết xuất kho.");
        }

        if (dto.ProductVariantId.HasValue && !dto.OutboundOrderItemId.HasValue)
        {
            var productBelongsToOutbound = await _context.OutboundOrderItems.AnyAsync(
                x => x.OutboundOrderId == dto.OutboundOrderId
                    && x.ProductVariantId == dto.ProductVariantId.Value
                    && !x.IsDeleted,
                cancellationToken);
            if (!productBelongsToOutbound)
                return ApiResponse.BadRequest(message: "Sản phẩm không thuộc phiếu xuất kho.");
        }

        if (dto.PaddyLotBagAllocationId.HasValue)
        {
            var bagAlloc = await _context.PaddyLotBagAllocations
                .Include(x => x.Bag)
                .FirstOrDefaultAsync(x => x.Id == dto.PaddyLotBagAllocationId.Value && !x.IsDeleted, cancellationToken);
                
            if (bagAlloc == null)
                return ApiResponse.BadRequest(message: "Bao lúa/gạo không tồn tại.");
                
            if (bagAlloc.ReferenceType != PaddyLotBagAllocationReferenceTypes.OutboundOrder || bagAlloc.ReferenceId != dto.OutboundOrderId)
                return ApiResponse.BadRequest(message: "Bao hàng truyền vào không thuộc phiếu xuất này.");
            var bagMatchesDeliveredLine = await _context.OutboundOrderItemAllocations.AnyAsync(
                x => x.OutboundOrderItem.OutboundOrderId == dto.OutboundOrderId
                    && (!dto.OutboundOrderItemId.HasValue || x.OutboundOrderItemId == dto.OutboundOrderItemId.Value)
                    && (!dto.ProductVariantId.HasValue || x.OutboundOrderItem.ProductVariantId == dto.ProductVariantId.Value)
                    && x.PaddyLotId == bagAlloc.Bag.LotId
                    && !x.IsDeleted,
                cancellationToken);
            if (!bagMatchesDeliveredLine)
                return ApiResponse.BadRequest(message: "Bao hàng không khớp với dòng hàng/lô đã giao của phiếu xuất.");
        }

        var feedback = new CustomerFeedback
        {
            SalesOrderId = dto.SalesOrderId,
            OutboundOrderId = dto.OutboundOrderId,
            OutboundOrderItemId = dto.OutboundOrderItemId,
            ProductVariantId = dto.ProductVariantId,
            PaddyLotBagAllocationId = dto.PaddyLotBagAllocationId,
            FeedbackType = dto.FeedbackType,
            Description = dto.Description,
            Severity = dto.Severity,
            ResolutionStatus = CustomerFeedbackStatus.Open,
            CreatedBy = GetCurrentUserId(),
            CreatedDate = DateTimeHelper.VietnamNow()
        };

        await _context.CustomerFeedbacks.AddAsync(feedback, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return ApiResponse.Created(data: feedback.Id, message: "Tạo khiếu nại thành công.");
    }

    public async Task<ApiResponse> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var feedback = await _context.CustomerFeedbacks
            .Include(f => f.SalesOrder)
            .Include(f => f.OutboundOrder)
            .Include(f => f.ProductVariant)
            .Include(f => f.PaddyLotBagAllocation)
                .ThenInclude(a => a.Bag)
            .Include(f => f.ResolvedByUser)
            .Include(f => f.CustomerReturnOrder)
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (feedback == null)
            return ApiResponse.NotFound(message: "Không tìm thấy khiếu nại.");

        var dto = MapToDto(feedback);
        return ApiResponse.Success(dto);
    }

    public async Task<ApiResponse> GetBySalesOrderAsync(int salesOrderId, CancellationToken cancellationToken = default)
    {
        var feedbacks = await _context.CustomerFeedbacks
            .Include(f => f.SalesOrder)
            .Include(f => f.OutboundOrder)
            .Include(f => f.ProductVariant)
            .Include(f => f.ResolvedByUser)
            .Include(f => f.CustomerReturnOrder)
            .Where(f => f.SalesOrderId == salesOrderId && !f.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ApiResponse.Success(feedbacks.Select(MapToDto));
    }

    public async Task<ApiResponse> GetByOutboundAsync(int outboundOrderId, CancellationToken cancellationToken = default)
    {
        var feedbacks = await _context.CustomerFeedbacks
            .Include(f => f.SalesOrder)
            .Include(f => f.OutboundOrder)
            .Include(f => f.ProductVariant)
            .Include(f => f.ResolvedByUser)
            .Include(f => f.CustomerReturnOrder)
            .Where(f => f.OutboundOrderId == outboundOrderId && !f.IsDeleted)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return ApiResponse.Success(feedbacks.Select(MapToDto));
    }

    public async Task<ApiResponse> ResolveAsync(int id, ResolveCustomerFeedbackDto dto, CancellationToken cancellationToken = default)
    {
        if ((dto.ResolutionStatus == CustomerFeedbackStatus.Resolved || dto.ResolutionStatus == CustomerFeedbackStatus.Rejected)
            && string.IsNullOrWhiteSpace(dto.ResolutionNote))
            return ApiResponse.BadRequest(message: "Ghi chú xử lý là bắt buộc khi kết thúc khiếu nại.");

        if (!CustomerFeedbackStatus.IsValid(dto.ResolutionStatus))
            return ApiResponse.BadRequest(message: "Trạng thái không hợp lệ.");

        var feedback = await _context.CustomerFeedbacks
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (feedback == null)
            return ApiResponse.NotFound(message: "Không tìm thấy khiếu nại.");

        feedback.ResolutionStatus = dto.ResolutionStatus;
        feedback.ResolutionNote = dto.ResolutionNote;
        
        if (dto.ResolutionStatus == CustomerFeedbackStatus.Resolved || dto.ResolutionStatus == CustomerFeedbackStatus.Rejected)
        {
            feedback.ResolvedAt = DateTimeHelper.VietnamNow();
            feedback.ResolvedBy = GetCurrentUserId();
        }

        if (dto.ResolutionStatus != CustomerFeedbackStatus.Resolved && dto.ResolutionStatus != CustomerFeedbackStatus.Rejected)
        {
            feedback.ResolvedAt = null;
            feedback.ResolvedBy = null;
        }

        feedback.UpdatedBy = GetCurrentUserId();
        feedback.LastModifiedDate = DateTimeHelper.VietnamNow();

        await _context.SaveChangesAsync(cancellationToken);
        return ApiResponse.Success(message: "Cập nhật trạng thái thành công.");
    }

    public async Task<ApiResponse> GetTraceInvestigationAsync(int id, CancellationToken cancellationToken = default)
    {
        var feedback = await _context.CustomerFeedbacks
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == id && !f.IsDeleted, cancellationToken);

        if (feedback == null)
            return ApiResponse.NotFound(message: "Không tìm thấy khiếu nại.");

        if (feedback.FeedbackType != CustomerFeedbackType.Quality)
            return ApiResponse.BadRequest(message: "Chỉ hỗ trợ truy vết cho khiếu nại về Chất lượng (QUALITY).");

        if (feedback.PaddyLotBagAllocationId.HasValue)
        {
            var bagAlloc = await _context.PaddyLotBagAllocations
                .Include(a => a.Bag)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == feedback.PaddyLotBagAllocationId.Value && !a.IsDeleted, cancellationToken);

            if (bagAlloc != null && bagAlloc.Bag.LotId > 0)
            {
                var trace = await _traceabilityService.GetByLotIdAsync(bagAlloc.Bag.LotId, cancellationToken: cancellationToken);
                return trace;
            }
        }
        else if (feedback.OutboundOrderItemId.HasValue)
        {
            var allocations = await _context.OutboundOrderItemAllocations
                .Include(a => a.PaddyLot)
                .Where(a => a.OutboundOrderItemId == feedback.OutboundOrderItemId.Value && a.PaddyLotId.HasValue && !a.IsDeleted)
                .Select(a => a.PaddyLotId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
                
            if (allocations.Count == 1)
            {
                var trace = await _traceabilityService.GetByLotIdAsync(allocations.First(), cancellationToken: cancellationToken);
                return trace;
            }
            else if (allocations.Count > 1)
            {
                return ApiResponse.Success(data: new { Warning = "Mặt hàng này xuất từ nhiều lô khác nhau, vui lòng chỉ định cụ thể bao hàng (Bag) để truy vết chính xác.", LotIds = allocations });
            }
        }
        else
        {
            var allocations = await _context.OutboundOrderItemAllocations
                .Where(a => a.OutboundOrderItem.OutboundOrderId == feedback.OutboundOrderId && a.PaddyLotId.HasValue && !a.IsDeleted)
                .Select(a => a.PaddyLotId.Value)
                .Distinct()
                .ToListAsync(cancellationToken);
                
            return ApiResponse.Success(data: new { Warning = "Phiếu xuất có nhiều lô hàng. Không thể truy vết cụ thể. Yêu cầu truyền ID bao lúa (Bag).", LotIds = allocations });
        }

        return ApiResponse.NotFound(message: "Không tìm thấy thông tin lô gạo để truy vết.");
    }

    public async Task<ApiResponse> GetPagedAsync(DTParameter parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.CustomerFeedbacks
            .Include(f => f.SalesOrder)
            .Include(f => f.OutboundOrder)
            .Where(f => !f.IsDeleted)
            .AsNoTracking();
            
        var search = parameters.Search?.Value?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(f => f.SalesOrder.SOCode.Contains(search) 
                                  || f.Description.Contains(search)
                                  || f.FeedbackType.Contains(search));
        }

        var total = await query.CountAsync(cancellationToken);
        
        var items = await query
            .OrderByDescending(f => f.CreatedDate)
            .Skip(parameters.Start)
            .Take(parameters.Length)
            .ToListAsync(cancellationToken);

        var data = items.Select(MapToDto).ToList();
        
        return ApiResponse.Success(new
        {
            draw = parameters.Draw,
            recordsTotal = total,
            recordsFiltered = total,
            data = data
        });
    }

    private CustomerFeedbackDto MapToDto(CustomerFeedback f)
    {
        return new CustomerFeedbackDto
        {
            Id = f.Id,
            SalesOrderId = f.SalesOrderId,
            SalesOrderCode = f.SalesOrder?.SOCode ?? string.Empty,
            OutboundOrderId = f.OutboundOrderId,
            OutboundOrderCode = $"OB-{f.OutboundOrderId:D5}",
            OutboundOrderItemId = f.OutboundOrderItemId,
            ProductVariantId = f.ProductVariantId,
            ProductVariantName = f.ProductVariant?.Name,
            SKU = f.ProductVariant?.SKU,
            PaddyLotBagAllocationId = f.PaddyLotBagAllocationId,
            FeedbackType = f.FeedbackType,
            Description = f.Description,
            Severity = f.Severity,
            ResolutionStatus = f.ResolutionStatus,
            ResolvedAt = f.ResolvedAt,
            ResolvedByName = f.ResolvedByUser?.LastName + " " + f.ResolvedByUser?.FirstName,
            ResolutionNote = f.ResolutionNote,
            CreatedDate = f.CreatedDate,
            CreatedByName = f.CreatedBy.ToString(),
            CustomerReturnOrderId = f.CustomerReturnOrder?.Id,
            CustomerReturnOrderCode = f.CustomerReturnOrder?.ReturnCode
        };
    }
}
