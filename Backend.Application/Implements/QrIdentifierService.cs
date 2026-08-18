using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.DependencyInjection.Extentions;
using Backend.Application.DTOs.QrCode;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class QrIdentifierService : IQrIdentifierService
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStorageService _storageService;

    public QrIdentifierService(
        IApplicationDbContext context, 
        IHttpContextAccessor httpContextAccessor,
        IStorageService storageService)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
        _storageService = storageService;
    }

    public async Task<EnsureQrResponseDto> EnsurePaddyLotQrCodeAsync(int paddyLotId, CancellationToken cancellationToken)
    {
        var lot = await _context.PaddyLots
            .FirstOrDefaultAsync(x => x.Id == paddyLotId && !x.IsDeleted, cancellationToken);
        if (lot == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy lô hàng với ID {paddyLotId}");
        }

        bool created = false;
        if (string.IsNullOrWhiteSpace(lot.QrCode))
        {
            created = true;
            int retries = 5;
            while (retries > 0)
            {
                var newCode = GenerateNewQrCode("PL-");
                var exists = await _context.PaddyLots.AnyAsync(x => x.QrCode == newCode, cancellationToken);
                if (!exists)
                {
                    lot.QrCode = newCode;
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                        break;
                    }
                    catch (DbUpdateException)
                    {
                        // unique constraint collision, retry
                    }
                }
                retries--;
                if (retries == 0)
                {
                    throw new InvalidOperationException("Không thể tạo mã QR duy nhất cho lô hàng sau nhiều lần thử.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(lot.QrImageUrl))
        {
            var payload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}";
            lot.QrImageUrl = await UploadQrCodeToCloudinaryAsync(payload, $"paddylot-qr-{lot.Id}.png", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new EnsureQrResponseDto
        {
            EntityType = "PADDY_LOT",
            EntityId = lot.Id,
            DisplayCode = lot.LotCode,
            QrCode = lot.QrCode,
            QrPayload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}",
            Created = created,
            QrImageUrl = lot.QrImageUrl,
            LabelUrl = $"/api/v1/paddy-lots/{lot.Id}/label"
        };
    }

    public async Task<EnsureQrResponseDto> EnsureLocationQrCodeAsync(int locationId, CancellationToken cancellationToken)
    {
        var loc = await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == locationId && !x.IsDeleted, cancellationToken);
        if (loc == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vị trí với ID {locationId}");
        }

        bool created = false;
        if (string.IsNullOrWhiteSpace(loc.QrCode))
        {
            created = true;
            int retries = 5;
            while (retries > 0)
            {
                var newCode = GenerateNewQrCode("LC-");
                var exists = await _context.Locations.AnyAsync(x => x.QrCode == newCode, cancellationToken);
                if (!exists)
                {
                    loc.QrCode = newCode;
                    try
                    {
                        await _context.SaveChangesAsync(cancellationToken);
                        break;
                    }
                    catch (DbUpdateException)
                    {
                        // unique constraint collision, retry
                    }
                }
                retries--;
                if (retries == 0)
                {
                    throw new InvalidOperationException("Không thể tạo mã QR duy nhất cho vị trí sau nhiều lần thử.");
                }
            }
        }

        if (string.IsNullOrWhiteSpace(loc.QrImageUrl))
        {
            var payload = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}";
            loc.QrImageUrl = await UploadQrCodeToCloudinaryAsync(payload, $"location-qr-{loc.Id}.png", cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return new EnsureQrResponseDto
        {
            EntityType = "LOCATION",
            EntityId = loc.Id,
            DisplayCode = FormatLocationCode(loc),
            QrCode = loc.QrCode,
            QrPayload = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}",
            Created = created,
            QrImageUrl = loc.QrImageUrl,
            LabelUrl = $"/api/v1/location/{loc.Id}/label"
        };
    }

    public async Task<EnsureQrResponseDto> RegeneratePaddyLotQrCodeAsync(int paddyLotId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Lý do tạo lại mã QR không được để trống.", nameof(reason));
        }

        var lot = await _context.PaddyLots
            .FirstOrDefaultAsync(x => x.Id == paddyLotId && !x.IsDeleted, cancellationToken);
        if (lot == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy lô hàng với ID {paddyLotId}");
        }

        var oldQrCode = lot.QrCode;
        string newCode = "";

        int retries = 5;
        while (retries > 0)
        {
            newCode = GenerateNewQrCode("PL-");
            var exists = await _context.PaddyLots.AnyAsync(x => x.QrCode == newCode, cancellationToken);
            if (!exists)
            {
                lot.QrCode = newCode;
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (DbUpdateException)
                {
                    // unique constraint collision, retry
                }
            }
            retries--;
            if (retries == 0)
            {
                throw new InvalidOperationException("Không thể tạo mã QR duy nhất mới cho lô hàng sau nhiều lần thử.");
            }
        }

        // Generate and upload the new QR image to Cloudinary
        var newPayload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{newCode}";
        lot.QrImageUrl = await UploadQrCodeToCloudinaryAsync(newPayload, $"paddylot-qr-{lot.Id}.png", cancellationToken);

        // Manual audit logging for QR Code regeneration details
        var audit = new AuditLog
        {
            Action = "REGENERATE_QR",
            TargetType = "PaddyLot",
            TargetId = lot.Id.ToString(),
            CreatedDate = DateTimeHelper.VietnamNow(),
            CreatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId(),
            IpAddress = _httpContextAccessor.HttpContext?.GetRemoteHostIpAddress(),
            UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString(),
            DataBefore = oldQrCode,
            DataAfter = newCode,
            Description = $"Thay đổi mã QR của lô hàng {lot.LotCode}. Lý do: {reason}"
        };
        await _context.AuditLogs.AddAsync(audit, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new EnsureQrResponseDto
        {
            EntityType = "PADDY_LOT",
            EntityId = lot.Id,
            DisplayCode = lot.LotCode,
            QrCode = lot.QrCode,
            QrPayload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}",
            Created = true,
            QrImageUrl = lot.QrImageUrl,
            LabelUrl = $"/api/v1/paddy-lots/{lot.Id}/label"
        };
    }

    public async Task<EnsureQrResponseDto> RegenerateLocationQrCodeAsync(int locationId, string reason, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Lý do tạo lại mã QR không được để trống.", nameof(reason));
        }

        var loc = await _context.Locations
            .FirstOrDefaultAsync(x => x.Id == locationId && !x.IsDeleted, cancellationToken);
        if (loc == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vị trí với ID {locationId}");
        }

        var oldQrCode = loc.QrCode;
        string newCode = "";

        int retries = 5;
        while (retries > 0)
        {
            newCode = GenerateNewQrCode("LC-");
            var exists = await _context.Locations.AnyAsync(x => x.QrCode == newCode, cancellationToken);
            if (!exists)
            {
                loc.QrCode = newCode;
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                    break;
                }
                catch (DbUpdateException)
                {
                    // unique constraint collision, retry
                }
            }
            retries--;
            if (retries == 0)
            {
                throw new InvalidOperationException("Không thể tạo mã QR duy nhất mới cho vị trí sau nhiều lần thử.");
            }
        }

        // Generate and upload the new QR image to Cloudinary
        var newPayload = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{newCode}";
        loc.QrImageUrl = await UploadQrCodeToCloudinaryAsync(newPayload, $"location-qr-{loc.Id}.png", cancellationToken);

        // Manual audit logging for QR Code regeneration details
        var audit = new AuditLog
        {
            Action = "REGENERATE_QR",
            TargetType = "Location",
            TargetId = loc.Id.ToString(),
            CreatedDate = DateTimeHelper.VietnamNow(),
            CreatedBy = _httpContextAccessor.HttpContext?.GetCurrentUserId(),
            IpAddress = _httpContextAccessor.HttpContext?.GetRemoteHostIpAddress(),
            UserAgent = _httpContextAccessor.HttpContext?.Request?.Headers["User-Agent"].ToString(),
            DataBefore = oldQrCode,
            DataAfter = newCode,
            Description = $"Thay đổi mã QR của vị trí {FormatLocationCode(loc)}. Lý do: {reason}"
        };
        await _context.AuditLogs.AddAsync(audit, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return new EnsureQrResponseDto
        {
            EntityType = "LOCATION",
            EntityId = loc.Id,
            DisplayCode = FormatLocationCode(loc),
            QrCode = loc.QrCode,
            QrPayload = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}",
            Created = true,
            QrImageUrl = loc.QrImageUrl,
            LabelUrl = $"/api/v1/location/{loc.Id}/label"
        };
    }

    private string GenerateNewQrCode(string prefix)
    {
        return $"{prefix}{Guid.NewGuid().ToString("N").ToUpper()}";
    }

    private string FormatLocationCode(Location loc)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(loc.ZoneName)) parts.Add(loc.ZoneName.Trim());
        if (!string.IsNullOrWhiteSpace(loc.ShelfRow)) parts.Add(loc.ShelfRow.Trim());
        if (!string.IsNullOrWhiteSpace(loc.ShelfLevel)) parts.Add(loc.ShelfLevel.Trim());
        if (!string.IsNullOrWhiteSpace(loc.SlotCode)) parts.Add(loc.SlotCode.Trim());
        return parts.Count > 0 ? string.Join("-", parts) : $"LOC-{loc.Id}";
    }

    private async Task<string> UploadQrCodeToCloudinaryAsync(string payload, string fileName, CancellationToken cancellationToken)
    {
        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng(payload, 10);
        using var qrStream = new System.IO.MemoryStream(qrBytes);
        IFormFile file = new FormFile(qrStream, 0, qrBytes.Length, "file", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };
        var uploadResult = await _storageService.UploadAsync(file, "qrcodes");
        if (!uploadResult.Success)
        {
            throw new InvalidOperationException($"Không thể tải hình ảnh QR lên Cloudinary: {uploadResult.ErrorMessage}");
        }
        return uploadResult.FilePath;
    }
}
