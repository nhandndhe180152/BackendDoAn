using System;
using Backend.Application.Constants;
using Backend.Application.DTOs.IotWeights;
using Backend.Application.Interfaces;
using Backend.Application.Mappings;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;

namespace Backend.Application.Implements;

public class IotWeightService : IIotWeightService
{
    private readonly IIotDeviceRepository _iotDeviceRepository;
    private readonly IIotWeightLogRepository _iotWeightLogRepository;

    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IInboundOrderItemRepository _inboundOrderItemRepository;
    private readonly IOutboundOrderItemRepository _outboundOrderItemRepository;
    private readonly IStockTakeItemRepository _stockTakeItemRepository;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public IotWeightService(
        IIotDeviceRepository iotDeviceRepository,
        IIotWeightLogRepository iotWeightLogRepository,
        IProductVariantRepository productVariantRepository,
        IInboundOrderItemRepository inboundOrderItemRepository,
        IOutboundOrderItemRepository outboundOrderItemRepository,
        IStockTakeItemRepository stockTakeItemRepository,
        IHttpContextAccessor httpContextAccessor)
    {
        _iotDeviceRepository = iotDeviceRepository;
        _iotWeightLogRepository = iotWeightLogRepository;
        _productVariantRepository = productVariantRepository;
        _inboundOrderItemRepository = inboundOrderItemRepository;
        _outboundOrderItemRepository = outboundOrderItemRepository;
        _stockTakeItemRepository = stockTakeItemRepository;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ApiResponse> ReceiveWeightAsync(
        ReceiveIotWeightDto dto,
        string? deviceKey,
        string? requestIpAddress)
    {
        try
        {
            var device = await _iotDeviceRepository.GetActiveByDeviceCodeAsync(dto.DeviceCode.Trim());
            if (device == null)
            {
                return ApiResponse.Unauthorized("Thiết bị IoT không hợp lệ hoặc đã bị khóa.", ApiCodeConstants.Common.Unauthorized);
            }

            if (string.IsNullOrWhiteSpace(deviceKey))
            {
                return ApiResponse.Unauthorized("Thiếu X-Device-Key.", ApiCodeConstants.Common.Unauthorized);
            }

            if (!DeviceKeyHelper.VerifyKey(deviceKey.Trim(), device.ApiKeyHash))
            {
                return ApiResponse.Unauthorized("X-Device-Key không hợp lệ.", ApiCodeConstants.Common.Unauthorized);
            }

            var weightKg = dto.WeightKg ?? dto.Weight;
            if (!weightKg.HasValue)
            {
                return ApiResponse.BadRequest("Thiếu dữ liệu khối lượng.", ApiCodeConstants.Common.BadRequest);
            }

            if (weightKg.Value > -0.05m && weightKg.Value < 0)
            {
                weightKg = 0;
            }

            if (weightKg.Value < 0)
            {
                return ApiResponse.BadRequest("Khối lượng không được âm.", ApiCodeConstants.Common.BadRequest);
            }

            var log = dto.ToEntity(device, weightKg.Value, requestIpAddress);

            await _iotWeightLogRepository.CreateAsync(log);

            var now = DateTime.Now;
            device.LastHeartbeat = now;
            device.IsOnline = true;
            device.LastModifiedDate = now;

            await _iotDeviceRepository.UpdateAsync(device);
            await _iotWeightLogRepository.SaveChangesAsync();

            return ApiResponse.Success(log.ToReceivedDto(device), "Nhận dữ liệu cân thành công.");
        }
        catch (Exception)
        {
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> GetLatestWeightAsync(string deviceCode)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(deviceCode))
            {
                return ApiResponse.BadRequest("DeviceCode không được để trống.", ApiCodeConstants.Common.BadRequest);
            }

            var device = await _iotDeviceRepository.GetActiveByDeviceCodeAsync(deviceCode.Trim());
            if (device == null)
            {
                return ApiResponse.NotFound("Không tìm thấy thiết bị IoT.", ApiCodeConstants.Common.NotFound);
            }

            var latestLog = await _iotWeightLogRepository.GetLatestByDeviceIdAsync(device.Id);
            if (latestLog == null)
            {
                return ApiResponse.NotFound("Thiết bị chưa có dữ liệu cân.", ApiCodeConstants.Common.NotFound);
            }

            return ApiResponse.Success(latestLog.ToLatestDto(device));
        }
        catch (Exception)
        {
            return ApiResponse.InternalServerError();
        }
    }

    public async Task<ApiResponse> AttachContextAsync(int weightLogId, AttachIotWeightContextDto dto)
    {
        await using var transaction = await _iotWeightLogRepository.BeginTransactionAsync();

        try
        {
            var referenceType = IotWeightReferenceTypeConstants.Normalize(dto.ReferenceType);

            var log = await _iotWeightLogRepository.GetByIdForAttachAsync(weightLogId);
            if (log == null)
            {
                return ApiResponse.NotFound("Không tìm thấy weight log.", ApiCodeConstants.Common.NotFound);
            }

            if (log.IsConfirmed)
            {
                return ApiResponse.BadRequest("Weight log này đã được xác nhận/gắn nghiệp vụ trước đó.", ApiCodeConstants.Common.BadRequest);
            }

            if (!log.IsStable)
            {
                return ApiResponse.BadRequest("Chỉ được gắn context cho dữ liệu cân đã ổn định.", ApiCodeConstants.Common.BadRequest);
            }

            if (log.WeightKg <= 0)
            {
                return ApiResponse.BadRequest("Chỉ được xác nhận dữ liệu cân có trọng lượng lớn hơn 0.", ApiCodeConstants.Common.BadRequest);
            }

            if (!IotWeightReferenceTypeConstants.IsValid(referenceType))
            {
                return ApiResponse.BadRequest("ReferenceType không hợp lệ.", ApiCodeConstants.Common.BadRequest);
            }

            decimal? referenceItemActualWeightKg = null;

            if (referenceType != IotWeightReferenceTypeConstants.Manual)
            {
                if (!dto.ProductVariantId.HasValue || dto.ProductVariantId <= 0)
                {
                    return ApiResponse.BadRequest("ProductVariantId không hợp lệ.", ApiCodeConstants.Common.BadRequest);
                }

                var productVariant = await _productVariantRepository.GetActiveByIdAsync(dto.ProductVariantId.Value);
                if (productVariant == null)
                {
                    return ApiResponse.NotFound("Không tìm thấy SKU/ProductVariant hoặc SKU đã bị khóa.", ApiCodeConstants.Common.NotFound);
                }
            }

            switch (referenceType)
            {
                case IotWeightReferenceTypeConstants.InboundOrder:
                    referenceItemActualWeightKg = await AttachInboundOrderContextAsync(dto, log);
                    break;

                case IotWeightReferenceTypeConstants.OutboundOrder:
                    referenceItemActualWeightKg = await AttachOutboundOrderContextAsync(dto, log);
                    break;

                case IotWeightReferenceTypeConstants.StockTake:
                    await ValidateStockTakeContextAsync(dto);
                    break;

                case IotWeightReferenceTypeConstants.Manual:
                    break;

                default:
                    return ApiResponse.BadRequest("ReferenceType không hợp lệ.", ApiCodeConstants.Common.BadRequest);
            }

            var now = DateTime.Now;
            int? currentUserId = _httpContextAccessor.HttpContext?.GetCurrentUserId();

            log.ProductVariantId = dto.ProductVariantId;
            log.ReferenceType = referenceType;
            log.ReferenceId = dto.ReferenceId;
            log.ReferenceItemId = dto.ReferenceItemId;
            log.IsConfirmed = true;
            log.ConfirmedBy = currentUserId;
            log.ConfirmedAt = now;
            log.UpdatedBy = currentUserId;
            log.LastModifiedDate = now;

            await _iotWeightLogRepository.UpdateAsync(log);
            await _iotWeightLogRepository.SaveChangesAsync();
            await _iotWeightLogRepository.EndTransactionAsync();

            return ApiResponse.Success(
                log.ToAttachedContextDto(referenceItemActualWeightKg),
                "Gắn context cho dữ liệu cân thành công.");
        }
        catch (InvalidOperationException ex)
        {
            await _iotWeightLogRepository.RollbackTransactionAsync();
            return ApiResponse.BadRequest(ex.Message, ApiCodeConstants.Common.BadRequest);
        }
        catch (KeyNotFoundException ex)
        {
            await _iotWeightLogRepository.RollbackTransactionAsync();
            return ApiResponse.NotFound(ex.Message, ApiCodeConstants.Common.NotFound);
        }
        catch (Exception)
        {
            await _iotWeightLogRepository.RollbackTransactionAsync();
            return ApiResponse.InternalServerError();
        }
    }

    private async Task<decimal?> AttachInboundOrderContextAsync(AttachIotWeightContextDto dto, Domain.Entities.IotWeightLog log)
    {
        if (!dto.ReferenceId.HasValue || !dto.ReferenceItemId.HasValue || !dto.ProductVariantId.HasValue)
        {
            throw new InvalidOperationException("Thiếu ReferenceId, ReferenceItemId hoặc ProductVariantId cho INBOUND_ORDER.");
        }

        var item = await _inboundOrderItemRepository.GetByIdForWeightAttachAsync(dto.ReferenceItemId.Value);
        if (item == null)
        {
            throw new KeyNotFoundException("Không tìm thấy InboundOrderItem.");
        }

        if (item.InboundOrderId != dto.ReferenceId.Value)
        {
            throw new InvalidOperationException("InboundOrderItem không thuộc InboundOrder được gửi lên.");
        }

        if (item.ProductVariantId != dto.ProductVariantId.Value)
        {
            throw new InvalidOperationException("SKU của InboundOrderItem không khớp với ProductVariantId được gửi lên.");
        }

        if (dto.UpdateReferenceItemActualWeight)
        {
            item.ActualWeightKg = log.WeightKg;
            item.UpdatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId();
            item.LastModifiedDate = DateTime.Now;

            await _inboundOrderItemRepository.UpdateAsync(item);
        }

        return item.ActualWeightKg;
    }

    private async Task<decimal?> AttachOutboundOrderContextAsync(AttachIotWeightContextDto dto, Domain.Entities.IotWeightLog log)
    {
        if (!dto.ReferenceId.HasValue || !dto.ReferenceItemId.HasValue || !dto.ProductVariantId.HasValue)
        {
            throw new InvalidOperationException("Thiếu ReferenceId, ReferenceItemId hoặc ProductVariantId cho OUTBOUND_ORDER.");
        }

        var item = await _outboundOrderItemRepository.GetByIdForWeightAttachAsync(dto.ReferenceItemId.Value);
        if (item == null)
        {
            throw new KeyNotFoundException("Không tìm thấy OutboundOrderItem.");
        }

        if (item.OutboundOrderId != dto.ReferenceId.Value)
        {
            throw new InvalidOperationException("OutboundOrderItem không thuộc OutboundOrder được gửi lên.");
        }

        if (item.ProductVariantId != dto.ProductVariantId.Value)
        {
            throw new InvalidOperationException("SKU của OutboundOrderItem không khớp với ProductVariantId được gửi lên.");
        }

        if (dto.UpdateReferenceItemActualWeight)
        {
            item.ActualWeightKg = log.WeightKg;
            item.UpdatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId();
            item.LastModifiedDate = DateTime.Now;

            await _outboundOrderItemRepository.UpdateAsync(item);
        }

        return item.ActualWeightKg;
    }

    private async Task ValidateStockTakeContextAsync(AttachIotWeightContextDto dto)
    {
        if (!dto.ReferenceId.HasValue || !dto.ReferenceItemId.HasValue || !dto.ProductVariantId.HasValue)
        {
            throw new InvalidOperationException("Thiếu ReferenceId, ReferenceItemId hoặc ProductVariantId cho STOCK_TAKE.");
        }

        var item = await _stockTakeItemRepository.GetByIdForWeightAttachAsync(dto.ReferenceItemId.Value);
        if (item == null)
        {
            throw new KeyNotFoundException("Không tìm thấy StockTakeItem.");
        }

        if (item.StockTakeId != dto.ReferenceId.Value)
        {
            throw new InvalidOperationException("StockTakeItem không thuộc StockTake được gửi lên.");
        }

        if (item.ProductVariantId != dto.ProductVariantId.Value)
        {
            throw new InvalidOperationException("SKU của StockTakeItem không khớp với ProductVariantId được gửi lên.");
        }
    }
}
