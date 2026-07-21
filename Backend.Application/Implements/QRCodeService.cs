using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Backend.Application.Constants;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.DTOs.QrCode;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
using Backend.Share.Extensions;
using Backend.Share.Helpers;
using iText.IO.Font;
using iText.IO.Image;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Backend.Application.Implements;

public class QRCodeService : IQRCodeService
{
    private readonly IProductVariantRepository _productVariantRepository;
    private readonly IStorageService _storageService;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IApplicationDbContext _context;
    private readonly IQrIdentifierService _qrIdentifierService;

    public QRCodeService(
        IProductVariantRepository productVariantRepository, 
        IStorageService storageService,
        IInventoryRepository inventoryRepository,
        IApplicationDbContext context,
        IQrIdentifierService qrIdentifierService)
    {
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
        _inventoryRepository = inventoryRepository;
        _context = context;
        _qrIdentifierService = qrIdentifierService;
    }

    public async Task<byte[]> GenerateQRCodeImageAsync(int productVariantId)
    {
        var variant = await _productVariantRepository.GetByIdAsync(productVariantId);
        if (variant == null || variant.IsDeleted)
        {
            throw new KeyNotFoundException($"Product variant with ID {productVariantId} not found.");
        }

        return QRCodeHelper.GenerateQRCodePng(variant.SKU, 10);
    }

    public async Task<byte[]> GenerateQRLabelPdfAsync(int productVariantId, float widthMm = 50f, float heightMm = 30f)
    {
        var variant = await _productVariantRepository
            .FindByCondition(x => x.Id == productVariantId && !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .FirstOrDefaultAsync();

        if (variant == null)
        {
            throw new KeyNotFoundException($"Product variant with ID {productVariantId} not found.");
        }

        // Fetch inventories to construct location text
        var inventories = await _inventoryRepository.GetByProductVariantAsync(productVariantId);
        var locations = inventories
            .Select(x => FormatLocation(x.Location))
            .Where(loc => !string.IsNullOrEmpty(loc))
            .Distinct()
            .ToList();

        string locationText = "Vị trí: --";
        if (locations.Any())
        {
            locationText = $"Vị trí: {string.Join(", ", locations)}";
        }

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                // Convert mm to points (1 inch = 25.4 mm = 72 points)
                float widthPt = (widthMm / 25.4f) * 72f;
                float heightPt = (heightMm / 25.4f) * 72f;

                PageSize labelSize = new PageSize(widthPt, heightPt);
                pdf.SetDefaultPageSize(labelSize);

                var doc = new Document(pdf);
                doc.SetMargins(3f, 3f, 3f, 3f);

                // Set font for Vietnamese support if available
                var font = GetVietnameseFont();
                if (font != null)
                {
                    doc.SetFont(font);
                }

                AddLabelContent(doc, variant, widthPt, heightPt, font, locationText);

                doc.Close();
            }
        }

        return ms.ToArray();
    }

    public async Task<byte[]> GenerateBulkQRLabelsPdfAsync(BatchQRLabelRequestDto request)
    {
        if (request == null || request.Items == null || !request.Items.Any())
        {
            throw new ArgumentException("Request items cannot be empty.", nameof(request));
        }

        var variantIds = request.Items.Select(x => x.ProductVariantId).ToList();
        var variants = await _productVariantRepository
            .FindByCondition(x => variantIds.Contains(x.Id) && !x.IsDeleted)
            .Include(x => x.Product)
            .Include(x => x.UnitOfMeasure)
            .ToDictionaryAsync(x => x.Id);

        // Fetch inventories for batch to construct location lookup map
        var inventories = await _inventoryRepository
            .FindByCondition(x => variantIds.Contains(x.ProductVariantId) && !x.IsDeleted)
            .Include(x => x.Location)
            .ToListAsync();

        var locationsMap = inventories
            .GroupBy(x => x.ProductVariantId)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => FormatLocation(x.Location))
                    .Where(loc => !string.IsNullOrEmpty(loc))
                    .Distinct()
                    .ToList()
            );

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                float widthPt = (request.WidthMm / 25.4f) * 72f;
                float heightPt = (request.HeightMm / 25.4f) * 72f;

                PageSize labelSize = new PageSize(widthPt, heightPt);
                pdf.SetDefaultPageSize(labelSize);

                var doc = new Document(pdf);
                doc.SetMargins(3f, 3f, 3f, 3f);

                var font = GetVietnameseFont();
                if (font != null)
                {
                    doc.SetFont(font);
                }

                bool isFirst = true;

                foreach (var item in request.Items)
                {
                    if (!variants.TryGetValue(item.ProductVariantId, out var variant))
                    {
                        continue;
                    }

                    locationsMap.TryGetValue(item.ProductVariantId, out var locations);
                    string locationText = "Vị trí: --";
                    if (locations != null && locations.Any())
                    {
                        locationText = $"Vị trí: {string.Join(", ", locations)}";
                    }

                    // Quantity là decimal (kg/túi); làm tròn sang int để in đúng số lượng nhãn.
                    // Math.Max(1,...) đảm bảo luôn in ít nhất 1 nhãn dù Quantity < 0.5.
                    var labelCount = (int)Math.Max(1, Math.Round((decimal)item.Quantity, MidpointRounding.AwayFromZero));
                    for (int i = 0; i < labelCount; i++)
                    {
                        if (!isFirst)
                        {
                            pdf.AddNewPage();
                        }
                        isFirst = false;

                        AddLabelContent(doc, variant, widthPt, heightPt, font, locationText);
                    }
                }

                doc.Close();
            }
        }

        return ms.ToArray();
    }

    public async Task<string> GenerateAndSaveQRUrlAsync(int productVariantId)
    {
        var variant = await _productVariantRepository.GetByIdAsync(productVariantId);
        if (variant == null || variant.IsDeleted)
        {
            throw new KeyNotFoundException($"Product variant with ID {productVariantId} not found.");
        }

        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng(variant.SKU, 10);

        using var qrStream = new MemoryStream(qrBytes);
        IFormFile file = new FormFile(qrStream, 0, qrBytes.Length, "file", $"qrcode-{variant.SKU}.png")
        {
            Headers = new HeaderDictionary(),
            ContentType = "image/png"
        };

        var uploadResult = await _storageService.UploadAsync(file, "qrcodes");
        if (!uploadResult.Success)
        {
            throw new InvalidOperationException($"Failed to upload QR Code to storage: {uploadResult.ErrorMessage}");
        }

        variant.QRCode = uploadResult.FilePath;
        await _productVariantRepository.UpdateAsync(variant);
        await _productVariantRepository.SaveChangesAsync();

        return variant.QRCode;
    }

    public async Task<int> SyncAllQRCodeUrlsAsync()
    {
        var variantsToSync = await _productVariantRepository
            .FindByCondition(x => !x.IsDeleted && string.IsNullOrEmpty(x.QRCode))
            .ToListAsync();

        int successCount = 0;
        foreach (var variant in variantsToSync)
        {
            try
            {
                await GenerateAndSaveQRUrlAsync(variant.Id);
                successCount++;
            }
            catch
            {
                // Log and continue
            }
        }

        return successCount;
    }

    private void AddLabelContent(Document doc, ProductVariant variant, float widthPt, float heightPt, PdfFont? font, string locationText)
    {
        // 2 Column layout: Left (QR Code - 40%), Right (Details - 60%)
        var table = new Table(UnitValue.CreatePercentArray(new float[] { 38f, 62f }))
            .SetWidth(UnitValue.CreatePercentValue(100f))
            .SetMarginTop(2f);

        if (font != null)
        {
            table.SetFont(font);
        }

        // 1. Left cell: QR Code Image
        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng(variant.SKU, 5);
        var qrImage = new Image(ImageDataFactory.Create(qrBytes))
            .SetAutoScale(true)
            .SetHorizontalAlignment(HorizontalAlignment.CENTER);

        var qrCell = new Cell()
            .Add(qrImage)
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .SetPadding(2f);

        table.AddCell(qrCell);

        // 2. Right cell: Details
        var detailsCell = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.TOP)
            .SetPaddingLeft(4f)
            .SetPaddingRight(2f);

        // Product Name (variant name or parent product name)
        string displayName = variant.Product?.Name ?? variant.Name;
        detailsCell.Add(new Paragraph(displayName)
            .SetFontSize(6f)
            .SetBold()
            .SetMultipliedLeading(0.9f)
            .SetMarginBottom(1f));

        // Attribute Values (e.g. Size: L, Color: Black)
        if (!string.IsNullOrEmpty(variant.AttributeValues))
        {
            detailsCell.Add(new Paragraph(variant.AttributeValues)
                .SetFontSize(4.5f)
                .SetMultipliedLeading(0.9f)
                .SetFontColor(ColorConstants.GRAY)
                .SetMarginBottom(1f));
        }

        // SKU code
        detailsCell.Add(new Paragraph($"SKU: {variant.SKU}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(1f));

        // Location info
        detailsCell.Add(new Paragraph(locationText)
            .SetFontSize(4.5f)
            .SetFontColor(ColorConstants.DARK_GRAY)
            .SetMarginBottom(1f));

        table.AddCell(detailsCell);
        doc.Add(table);
    }

    private PdfFont? GetVietnameseFont()
    {
        string[] paths = {
            // Windows standard font paths (various casings)
            @"C:\Windows\Fonts\Arial.ttf",
            @"C:\Windows\Fonts\arial.ttf",
            @"C:\Windows\Fonts\Calibri.ttf",
            @"C:\Windows\Fonts\calibri.ttf",
            @"C:\Windows\Fonts\Tahoma.ttf",
            @"C:\Windows\Fonts\tahoma.ttf",
            @"C:\Windows\Fonts\Times.ttf",
            @"C:\Windows\Fonts\times.ttf",
            @"C:\Windows\Fonts\SegoeUI.ttf",
            @"C:\Windows\Fonts\segoeui.ttf",
            
            // Linux standard font paths (DejaVu, Liberation, FreeSans, etc.)
            "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
            "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf",
            "/usr/share/fonts/truetype/freefont/FreeSans.ttf",
            
            // macOS standard font paths
            "/Library/Fonts/Arial.ttf",
            "/Library/Fonts/Arial Unicode.ttf",
            "/System/Library/Fonts/Supplemental/Arial.ttf",
            "/System/Library/Fonts/Arial.ttf"
        };

        foreach (var path in paths)
        {
            if (File.Exists(path))
            {
                try
                {
                    return PdfFontFactory.CreateFont(path, PdfEncodings.IDENTITY_H);
                }
                catch
                {
                    // Continue scan if failed
                }
            }
        }

        return null;
    }

    private string FormatLocation(Location? location)
    {
        if (location == null) return string.Empty;

        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(location.ZoneName))
        {
            var zone = location.ZoneName.Trim();
            parts.Add(zone.StartsWith("Khu", StringComparison.OrdinalIgnoreCase) ? zone : $"Khu {zone}");
        }
        if (!string.IsNullOrWhiteSpace(location.ShelfRow))
        {
            var row = location.ShelfRow.Trim();
            parts.Add(row.StartsWith("Dãy", StringComparison.OrdinalIgnoreCase) || row.StartsWith("Hàng", StringComparison.OrdinalIgnoreCase) ? row : $"Dãy {row}");
        }
        if (!string.IsNullOrWhiteSpace(location.ShelfLevel))
        {
            var level = location.ShelfLevel.Trim();
            parts.Add(level.StartsWith("Tầng", StringComparison.OrdinalIgnoreCase) ? level : $"Tầng {level}");
        }
        if (!string.IsNullOrWhiteSpace(location.SlotCode))
        {
            var slot = location.SlotCode.Trim();
            parts.Add(slot.StartsWith("Ô", StringComparison.OrdinalIgnoreCase) ? slot : $"Ô {slot}");
        }

        return string.Join(" - ", parts);
    }

    public async Task<byte[]> GeneratePaddyLotQRImageAsync(int id, int size, CancellationToken cancellationToken = default)
    {
        var ensureRes = await _qrIdentifierService.EnsurePaddyLotQrCodeAsync(id, cancellationToken);
        return QRCodeHelper.GenerateQRCodePng(ensureRes.QrPayload, size);
    }

    public async Task<byte[]> GenerateLocationQRImageAsync(int id, int size, CancellationToken cancellationToken = default)
    {
        var ensureRes = await _qrIdentifierService.EnsureLocationQrCodeAsync(id, cancellationToken);
        return QRCodeHelper.GenerateQRCodePng(ensureRes.QrPayload, size);
    }

    public async Task<byte[]> GeneratePaddyLotLabelPdfAsync(int id, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var lot = await _context.PaddyLots
            .Include(x => x.ProductVariant)
            .Include(x => x.RiceVariety)
            .Include(x => x.Warehouse)
            .Include(x => x.Status)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (lot == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy lô hàng với ID {id}");
        }

        await _qrIdentifierService.EnsurePaddyLotQrCodeAsync(id, cancellationToken);

        var (wMm, hMm) = GetDimensionsFromTemplate(templateCode);
        float widthPt = (wMm / 25.4f) * 72f;
        float heightPt = (hMm / 25.4f) * 72f;

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetDefaultPageSize(new PageSize(widthPt, heightPt));
                var doc = new Document(pdf);
                doc.SetMargins(2f, 2f, 2f, 2f);

                var font = GetVietnameseFont();
                if (font != null) doc.SetFont(font);

                for (int i = 0; i < copies; i++)
                {
                    if (i > 0) pdf.AddNewPage();
                    AddPaddyLotLabelContent(doc, lot, widthPt, heightPt, font);
                }
                doc.Close();
            }
        }
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateLocationLabelPdfAsync(int id, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var loc = await _context.Locations
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (loc == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy vị trí với ID {id}");
        }

        await _qrIdentifierService.EnsureLocationQrCodeAsync(id, cancellationToken);

        var (wMm, hMm) = GetDimensionsFromTemplate(templateCode);
        float widthPt = (wMm / 25.4f) * 72f;
        float heightPt = (hMm / 25.4f) * 72f;

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetDefaultPageSize(new PageSize(widthPt, heightPt));
                var doc = new Document(pdf);
                doc.SetMargins(2f, 2f, 2f, 2f);

                var font = GetVietnameseFont();
                if (font != null) doc.SetFont(font);

                for (int i = 0; i < copies; i++)
                {
                    if (i > 0) pdf.AddNewPage();
                    AddLocationLabelContent(doc, loc, widthPt, heightPt, font);
                }
                doc.Close();
            }
        }
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateBulkPaddyLotLabelsPdfAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            throw new ArgumentException("Danh sách ID không được rỗng.", nameof(ids));
        }

        var distinctIds = ids.Distinct().ToList();
        var lots = await _context.PaddyLots
            .Include(x => x.ProductVariant)
            .Include(x => x.RiceVariety)
            .Include(x => x.Warehouse)
            .Include(x => x.Status)
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        if (lots.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều lô hàng không tồn tại hoặc đã bị xóa.");
        }

        foreach (var lot in lots)
        {
            await _qrIdentifierService.EnsurePaddyLotQrCodeAsync(lot.Id, cancellationToken);
        }

        var (wMm, hMm) = GetDimensionsFromTemplate(templateCode);
        float widthPt = (wMm / 25.4f) * 72f;
        float heightPt = (hMm / 25.4f) * 72f;

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetDefaultPageSize(new PageSize(widthPt, heightPt));
                var doc = new Document(pdf);
                doc.SetMargins(2f, 2f, 2f, 2f);

                var font = GetVietnameseFont();
                if (font != null) doc.SetFont(font);

                bool isFirst = true;
                foreach (var lot in lots)
                {
                    for (int i = 0; i < copies; i++)
                    {
                        if (!isFirst) pdf.AddNewPage();
                        isFirst = false;
                        AddPaddyLotLabelContent(doc, lot, widthPt, heightPt, font);
                    }
                }
                doc.Close();
            }
        }
        return ms.ToArray();
    }

    public async Task<byte[]> GenerateBulkLocationLabelsPdfAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            throw new ArgumentException("Danh sách ID không được rỗng.", nameof(ids));
        }

        var distinctIds = ids.Distinct().ToList();
        var locs = await _context.Locations
            .Include(x => x.Warehouse)
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted)
            .ToListAsync(cancellationToken);

        if (locs.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều vị trí không tồn tại hoặc đã bị xóa.");
        }

        foreach (var loc in locs)
        {
            await _qrIdentifierService.EnsureLocationQrCodeAsync(loc.Id, cancellationToken);
        }

        var (wMm, hMm) = GetDimensionsFromTemplate(templateCode);
        float widthPt = (wMm / 25.4f) * 72f;
        float heightPt = (hMm / 25.4f) * 72f;

        using var ms = new MemoryStream();
        using (var writer = new PdfWriter(ms))
        {
            using (var pdf = new PdfDocument(writer))
            {
                pdf.SetDefaultPageSize(new PageSize(widthPt, heightPt));
                var doc = new Document(pdf);
                doc.SetMargins(2f, 2f, 2f, 2f);

                var font = GetVietnameseFont();
                if (font != null) doc.SetFont(font);

                bool isFirst = true;
                foreach (var loc in locs)
                {
                    for (int i = 0; i < copies; i++)
                    {
                        if (!isFirst) pdf.AddNewPage();
                        isFirst = false;
                        AddLocationLabelContent(doc, loc, widthPt, heightPt, font);
                    }
                }
                doc.Close();
            }
        }
        return ms.ToArray();
    }

    private (float WidthMm, float HeightMm) GetDimensionsFromTemplate(string templateCode)
    {
        return templateCode?.ToUpper() switch
        {
            "SMALL" => (50f, 30f),
            "LARGE" => (100f, 70f),
            _ => (70f, 50f) // MEDIUM as default
        };
    }

    private void AddPaddyLotLabelContent(Document doc, PaddyLot lot, float widthPt, float heightPt, PdfFont? font)
    {
        // Title: LÔ HÀNG hoặc LÔ LÚA/GẠO
        string titleText = lot.LotType?.ToUpper() switch
        {
            "PADDY" => "LÔ LÚA",
            "RICE" => "LÔ GẠO",
            _ => "LÔ PHỤ PHẨM"
        };

        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 100f }))
            .SetWidth(UnitValue.CreatePercentValue(100f));
        
        var titleParagraph = new Paragraph(titleText)
            .SetFontSize(8f)
            .SetBold()
            .SetTextAlignment(TextAlignment.CENTER)
            .SetMarginBottom(1f);
        
        headerTable.AddCell(new Cell().Add(titleParagraph).SetBorder(Border.NO_BORDER));
        doc.Add(headerTable);

        // 2 column layout: Left (QR code), Right (Details)
        var bodyTable = new Table(UnitValue.CreatePercentArray(new float[] { 35f, 65f }))
            .SetWidth(UnitValue.CreatePercentValue(100f))
            .SetMarginTop(1f);

        // QR Code Image
        var payload = $"STOCKLITE|1|PADDY_LOT|{lot.QrCode}";
        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng(payload, 5);
        var qrImage = new Image(ImageDataFactory.Create(qrBytes))
            .SetAutoScale(true)
            .SetHorizontalAlignment(HorizontalAlignment.CENTER);

        var qrCell = new Cell().Add(qrImage)
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .SetPadding(1f);
        bodyTable.AddCell(qrCell);

        // Details Column
        var detailsCell = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.TOP)
            .SetPaddingLeft(2f);

        // Lot Code
        detailsCell.Add(new Paragraph($"Mã Lô: {lot.LotCode}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(0.5f));

        // SKU / Variant Name
        detailsCell.Add(new Paragraph($"Sản phẩm: {lot.ProductVariant?.Name ?? "N/A"}")
            .SetFontSize(4.5f)
            .SetMultipliedLeading(0.9f)
            .SetMarginBottom(0.5f));

        // Rice Variety
        if (lot.RiceVariety != null)
        {
            detailsCell.Add(new Paragraph($"Giống: {lot.RiceVariety.Name} ({lot.RiceVariety.Code})")
                .SetFontSize(4.5f)
                .SetMarginBottom(0.5f));
        }

        // Inbound Date
        detailsCell.Add(new Paragraph($"Ngày nhập: {lot.InboundDate:dd/MM/yyyy}")
            .SetFontSize(4.5f)
            .SetMarginBottom(0.5f));

        // Warehouse Info
        detailsCell.Add(new Paragraph($"Kho: {lot.Warehouse?.Name ?? "N/A"}")
            .SetFontSize(4.5f)
            .SetMarginBottom(0.5f));

        // If lot is quarantined at print time
        if (lot.Status?.Code == LotStatusCodeConstants.Quarantine)
        {
            detailsCell.Add(new Paragraph("ĐANG CÁCH LY")
                .SetFontSize(5f)
                .SetBold()
                .SetFontColor(ColorConstants.RED)
                .SetMarginBottom(0.5f));
        }

        // Footer / Instruction
        detailsCell.Add(new Paragraph("Quét mã để xem thông tin hiện tại")
            .SetFontSize(4f)
            .SetFontColor(ColorConstants.GRAY)
            .SetItalic()
            .SetMarginBottom(0.5f));

        // Printed At
        detailsCell.Add(new Paragraph($"In lúc: {DateTimeHelper.VietnamNow():dd/MM/yyyy HH:mm}")
            .SetFontSize(3.5f)
            .SetFontColor(ColorConstants.GRAY));

        bodyTable.AddCell(detailsCell);
        doc.Add(bodyTable);
    }

    private void AddLocationLabelContent(Document doc, Location loc, float widthPt, float heightPt, PdfFont? font)
    {
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 100f }))
            .SetWidth(UnitValue.CreatePercentValue(100f));
        
        var titleParagraph = new Paragraph("VỊ TRÍ KHO")
            .SetFontSize(8f)
            .SetBold()
            .SetTextAlignment(TextAlignment.CENTER)
            .SetMarginBottom(1f);
        
        headerTable.AddCell(new Cell().Add(titleParagraph).SetBorder(Border.NO_BORDER));
        doc.Add(headerTable);

        // 2 column layout: Left (QR code), Right (Details)
        var bodyTable = new Table(UnitValue.CreatePercentArray(new float[] { 35f, 65f }))
            .SetWidth(UnitValue.CreatePercentValue(100f))
            .SetMarginTop(1f);

        // QR Code Image
        var payload = $"STOCKLITE|1|LOCATION|{loc.QrCode}";
        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng(payload, 5);
        var qrImage = new Image(ImageDataFactory.Create(qrBytes))
            .SetAutoScale(true)
            .SetHorizontalAlignment(HorizontalAlignment.CENTER);

        var qrCell = new Cell().Add(qrImage)
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.MIDDLE)
            .SetPadding(1f);
        bodyTable.AddCell(qrCell);

        // Details Column
        var detailsCell = new Cell()
            .SetBorder(Border.NO_BORDER)
            .SetVerticalAlignment(VerticalAlignment.TOP)
            .SetPaddingLeft(2f);

        // Warehouse Info
        detailsCell.Add(new Paragraph($"Kho: {loc.Warehouse?.Name ?? "N/A"}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(0.5f));

        // Location coordinates
        detailsCell.Add(new Paragraph($"Khu vực: {loc.ZoneName ?? "--"}")
            .SetFontSize(4.5f)
            .SetMarginBottom(0.5f));

        detailsCell.Add(new Paragraph($"Dãy: {loc.ShelfRow ?? "--"} | Tầng: {loc.ShelfLevel ?? "--"}")
            .SetFontSize(4.5f)
            .SetMarginBottom(0.5f));

        detailsCell.Add(new Paragraph($"Ô: {loc.SlotCode ?? "--"}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(0.5f));

        // Quarantine check
        if (loc.IsQuarantine)
        {
            detailsCell.Add(new Paragraph("KHU VỰC CÁCH LY")
                .SetFontSize(5f)
                .SetBold()
                .SetFontColor(ColorConstants.RED)
                .SetMarginBottom(0.5f));
        }

        // Footer / Instruction
        detailsCell.Add(new Paragraph("Quét mã để xem vị trí và tồn hiện tại")
            .SetFontSize(4f)
            .SetFontColor(ColorConstants.GRAY)
            .SetItalic()
            .SetMarginBottom(0.5f));

        // Printed At
        detailsCell.Add(new Paragraph($"In lúc: {DateTimeHelper.VietnamNow():dd/MM/yyyy HH:mm}")
            .SetFontSize(3.5f)
            .SetFontColor(ColorConstants.GRAY));

        bodyTable.AddCell(detailsCell);
        doc.Add(bodyTable);
    }

    public async Task<QrResolveResponseDto> ResolveQrAsync(QrResolveRequestDto request, CancellationToken cancellationToken = default)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Payload))
        {
            throw new ArgumentException("Payload quét QR không được để trống.", nameof(request));
        }

        var parts = request.Payload.Split('|');
        if (parts.Length != 4 || parts[0] != "STOCKLITE")
        {
            throw new ArgumentException("Payload QR không đúng định dạng STOCKLITE.", nameof(request));
        }

        if (parts[1] != "1")
        {
            throw new ArgumentException("Phiên bản mã QR không được hỗ trợ.", nameof(request));
        }

        var entityType = parts[2].ToUpper();
        var qrCode = parts[3];

        if (entityType == "PADDY_LOT")
        {
            var lot = await _context.PaddyLots
                .Include(x => x.ProductVariant)
                .Include(x => x.RiceVariety)
                .Include(x => x.Warehouse)
                .Include(x => x.Status)
                .FirstOrDefaultAsync(x => x.QrCode == qrCode, cancellationToken);

            if (lot == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin lô hàng tương ứng với mã QR.");
            }

            if (lot.IsDeleted)
            {
                throw new InvalidOperationException("Lô hàng này đã bị xóa trên hệ thống.");
            }

            var isQuarantined = lot.Status?.Code == LotStatusCodeConstants.Quarantine;

            var response = new QrResolveResponseDto
            {
                EntityType = "PADDY_LOT",
                EntityId = lot.Id,
                QrCode = lot.QrCode,
                DisplayCode = lot.LotCode,
                LotType = lot.LotType,
                RemainingWeightKg = lot.RemainingWeightKg,
                IsQuarantined = isQuarantined,
                ProductVariant = new QrProductVariantDto
                {
                    Id = lot.ProductVariant.Id,
                    Sku = lot.ProductVariant.SKU,
                    Name = lot.ProductVariant.Name
                },
                RiceVariety = lot.RiceVariety == null ? null : new QrRiceVarietyDto
                {
                    Id = lot.RiceVariety.Id,
                    Code = lot.RiceVariety.Code,
                    Name = lot.RiceVariety.Name
                },
                Warehouse = lot.Warehouse == null ? null : new QrWarehouseDto
                {
                    Id = lot.Warehouse.Id,
                    Code = lot.Warehouse.Code,
                    Name = lot.Warehouse.Name
                },
                Status = lot.Status == null ? null : new QrLotStatusDto
                {
                    Id = lot.Status.Id,
                    Name = lot.Status.Name,
                    IsSellable = lot.Status.IsSellable
                },
                NavigationTarget = new QrNavigationTargetDto
                {
                    Type = "PADDY_LOT_DETAIL",
                    Id = lot.Id
                }
            };

            if (request.Context != null)
            {
                response.ValidationResult = ValidatePaddyLotContext(lot, request.Context);
            }

            return response;
        }
        else if (entityType == "LOCATION")
        {
            var loc = await _context.Locations
                .Include(x => x.Warehouse)
                .FirstOrDefaultAsync(x => x.QrCode == qrCode, cancellationToken);

            if (loc == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin vị trí tương ứng với mã QR.");
            }

            if (loc.IsDeleted)
            {
                throw new InvalidOperationException("Vị trí lưu kho này đã bị xóa trên hệ thống.");
            }

            if (!loc.IsActive)
            {
                throw new InvalidOperationException("Vị trí lưu kho này đã ngưng hoạt động.");
            }

            var freeCapacity = loc.MaxCapacity.HasValue ? Math.Max(0m, loc.MaxCapacity.Value - loc.CurrentOccupancy) : (decimal?)null;

            var response = new QrResolveResponseDto
            {
                EntityType = "LOCATION",
                EntityId = loc.Id,
                QrCode = loc.QrCode,
                DisplayCode = FormatLocation(loc),
                ZoneName = loc.ZoneName,
                ShelfRow = loc.ShelfRow,
                ShelfLevel = loc.ShelfLevel,
                SlotCode = loc.SlotCode,
                MaxCapacityKg = loc.MaxCapacity,
                CurrentOccupancyKg = loc.CurrentOccupancy,
                FreeCapacityKg = freeCapacity,
                IsQuarantine = loc.IsQuarantine,
                IsActive = loc.IsActive,
                Warehouse = loc.Warehouse == null ? null : new QrWarehouseDto
                {
                    Id = loc.Warehouse.Id,
                    Code = loc.Warehouse.Code,
                    Name = loc.Warehouse.Name
                },
                NavigationTarget = new QrNavigationTargetDto
                {
                    Type = "LOCATION_DETAIL",
                    Id = loc.Id
                }
            };

            if (request.Context != null)
            {
                response.ValidationResult = ValidateLocationContext(loc, request.Context);
            }

            return response;
        }

        throw new ArgumentException("Kiểu đối tượng trong mã QR không hợp lệ.", nameof(request));
    }

    private QrContextValidationResultDto ValidatePaddyLotContext(PaddyLot lot, QrContextDto context)
    {
        var op = context.Operation?.ToUpper();
        if (op == "MILLING_INPUT")
        {
            if (lot.LotType?.ToUpper() != "PADDY")
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_PRODUCT_MISMATCH",
                    ErrorMessage = "Chỉ chấp nhận lô lúa cho hoạt động xay xát."
                };
            }
            if (lot.Status?.Code == LotStatusCodeConstants.Quarantine)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_QUARANTINED",
                    ErrorMessage = "Lô lúa đang bị cách ly, không được xay xát."
                };
            }
            if (lot.RemainingWeightKg <= 0)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_NOT_AVAILABLE",
                    ErrorMessage = "Lô hàng đã hết hoặc không còn khối lượng."
                };
            }
        }
        else if (op == "OUTBOUND_PICKING")
        {
            if (lot.Status?.Code == LotStatusCodeConstants.Quarantine)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_QUARANTINED",
                    ErrorMessage = "Lô hàng đang bị cách ly, không được phép xuất kho."
                };
            }
            if (lot.RemainingWeightKg <= 0)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_NOT_AVAILABLE",
                    ErrorMessage = "Lô hàng không còn tồn để xuất kho."
                };
            }
            if (context.ReferenceId.HasValue && lot.ProductVariantId != context.ReferenceId.Value)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOT_PRODUCT_MISMATCH",
                    ErrorMessage = "Biến thể sản phẩm của lô không khớp với biến thể yêu cầu xuất kho."
                };
            }
        }
        else if (op == "STOCKTAKE")
        {
            if (context.WarehouseId.HasValue && lot.WarehouseId != context.WarehouseId.Value)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "QR_CONTEXT_MISMATCH",
                    ErrorMessage = "Lô lúa này thuộc kho khác với kho đang kiểm kê."
                };
            }
        }

        return new QrContextValidationResultDto { Success = true };
    }

    private QrContextValidationResultDto ValidateLocationContext(Location loc, QrContextDto context)
    {
        var op = context.Operation?.ToUpper();
        if (op == "STORE_IN")
        {
            if (!loc.IsActive)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "QR_ENTITY_INACTIVE",
                    ErrorMessage = "Vị trí lưu trữ hiện đang ngưng hoạt động."
                };
            }
            if (context.WarehouseId.HasValue && loc.WarehouseId != context.WarehouseId.Value)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOCATION_WRONG_WAREHOUSE",
                    ErrorMessage = "Vị trí đã quét không thuộc về kho chỉ định của phiếu nhập."
                };
            }
            if (context.ReferenceId.HasValue) // representing PaddyLotId
            {
                var lot = _context.PaddyLots.Include(x => x.Status).FirstOrDefault(x => x.Id == context.ReferenceId.Value);
                if (lot != null)
                {
                    var lotIsQuarantine = lot.Status?.Code == LotStatusCodeConstants.Quarantine;
                    if (lotIsQuarantine && !loc.IsQuarantine)
                    {
                        return new QrContextValidationResultDto
                        {
                            Success = false,
                            ErrorCode = "LOCATION_QUARANTINE_REQUIRED",
                            ErrorMessage = "Lô hàng đang bị cách ly. Yêu cầu nhập vào khu vực cách ly."
                        };
                    }
                    if (!lotIsQuarantine && loc.IsQuarantine)
                    {
                        return new QrContextValidationResultDto
                        {
                            Success = false,
                            ErrorCode = "LOCATION_NORMAL_REQUIRED",
                            ErrorMessage = "Lô hàng bình thường. Không được nhập vào khu vực cách ly."
                        };
                    }

                    if (loc.MaxCapacity.HasValue && loc.CurrentOccupancy + lot.RemainingWeightKg > loc.MaxCapacity.Value)
                    {
                        return new QrContextValidationResultDto
                        {
                            Success = false,
                            ErrorCode = "LOCATION_INSUFFICIENT_CAPACITY",
                            ErrorMessage = $"Vị trí không đủ sức chứa cho lô hàng này. Còn trống: {Math.Max(0m, loc.MaxCapacity.Value - loc.CurrentOccupancy)} kg."
                        };
                    }
                }
            }
        }
        else if (op == "STOCK_TRANSFER")
        {
            if (!loc.IsActive)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "QR_ENTITY_INACTIVE",
                    ErrorMessage = "Vị trí đích hiện đang ngưng hoạt động."
                };
            }
            if (context.WarehouseId.HasValue && loc.WarehouseId != context.WarehouseId.Value)
            {
                return new QrContextValidationResultDto
                {
                    Success = false,
                    ErrorCode = "LOCATION_WRONG_WAREHOUSE",
                    ErrorMessage = "Vị trí đích không nằm trong kho điều chuyển."
                };
            }
        }

        return new QrContextValidationResultDto { Success = true };
    }
}
