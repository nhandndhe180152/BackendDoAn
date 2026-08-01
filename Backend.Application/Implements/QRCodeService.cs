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
using Backend.Application.DependencyInjection.Extentions;
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
    private readonly IHttpContextAccessor _httpContextAccessor;

    public QRCodeService(
        IProductVariantRepository productVariantRepository, 
        IStorageService storageService,
        IInventoryRepository inventoryRepository,
        IApplicationDbContext context,
        IQrIdentifierService qrIdentifierService,
        IHttpContextAccessor httpContextAccessor)
    {
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
        _inventoryRepository = inventoryRepository;
        _context = context;
        _qrIdentifierService = qrIdentifierService;
        _httpContextAccessor = httpContextAccessor;
    }


    public async Task<byte[]> GenerateQRCodeImageAsync(int productVariantId)
    {
        var variant = await _productVariantRepository.GetByIdAsync(productVariantId);
        if (variant == null || variant.IsDeleted)
        {
            throw new KeyNotFoundException($"Product variant with ID {productVariantId} not found.");
        }

        return QRCodeHelper.GenerateQRCodePng($"STOCKLITE|1|SKU|{variant.SKU}", 10);
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

        var pdfBytes = ms.ToArray();
        await LogPrintAuditAsync("ProductVariant", productVariantId.ToString(), 1, $"In nhãn SKU biến thể {variant.SKU}", CancellationToken.None);
        return pdfBytes;
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

        var bulkBytes = ms.ToArray();
        var ids = request.Items.Select(x => x.ProductVariantId).ToList();
        await LogPrintAuditAsync("ProductVariant", string.Join(",", ids), 1, $"In hàng loạt nhãn SKU biến thể sản phẩm ({request.Items.Count} mặt hàng)", CancellationToken.None);
        return bulkBytes;
    }

    public async Task<string> GenerateAndSaveQRUrlAsync(int productVariantId)
    {
        var variant = await _productVariantRepository.GetByIdAsync(productVariantId);
        if (variant == null || variant.IsDeleted)
        {
            throw new KeyNotFoundException($"Product variant with ID {productVariantId} not found.");
        }

        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng($"STOCKLITE|1|SKU|{variant.SKU}", 10);

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
        byte[] qrBytes = QRCodeHelper.GenerateQRCodePng($"STOCKLITE|1|SKU|{variant.SKU}", 5);
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
        var pdfBytes = ms.ToArray();
        await LogPrintAuditAsync("PaddyLot", id.ToString(), copies, $"In {copies} nhãn lô hàng {lot.LotCode}", cancellationToken);
        return pdfBytes;
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
        var locPdfBytes = ms.ToArray();
        await LogPrintAuditAsync("Location", id.ToString(), copies, $"In {copies} nhãn vị trí {FormatLocation(loc)}", cancellationToken);
        return locPdfBytes;
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
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted).OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (lots.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều lô hàng không tồn tại hoặc đã bị xóa.");
        }

        // Batch-ensure QrCode (chuỗi định danh) cho các lô chưa có — không upload Cloudinary vì PDF sinh QR tại chỗ bằng QRCodeHelper.
        var lotsWithoutQr = lots.Where(x => string.IsNullOrWhiteSpace(x.QrCode)).ToList();
        if (lotsWithoutQr.Count > 0)
        {
            foreach (var lot in lotsWithoutQr)
                lot.QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper();
            await _context.SaveChangesAsync(cancellationToken);
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
        var pdfBytes = ms.ToArray();
        await LogPrintAuditAsync("PaddyLot", string.Join(",", distinctIds), copies, $"In hàng loạt nhãn lô hàng ({lots.Count} nhãn, {copies} bản/nhãn)", cancellationToken);
        return pdfBytes;
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
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted).OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (locs.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều vị trí không tồn tại hoặc đã bị xóa.");
        }

        // Batch-ensure QrCode (chuỗi định danh) cho các vị trí chưa có — không upload Cloudinary vì PDF sinh QR tại chỗ bằng QRCodeHelper.
        var locsWithoutQr = locs.Where(x => string.IsNullOrWhiteSpace(x.QrCode)).ToList();
        if (locsWithoutQr.Count > 0)
        {
            foreach (var loc in locsWithoutQr)
                loc.QrCode = "LC-" + Guid.NewGuid().ToString("N").ToUpper();
            await _context.SaveChangesAsync(cancellationToken);
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
        var locPdfBytes = ms.ToArray();
        await LogPrintAuditAsync("Location", string.Join(",", distinctIds), copies, $"In hàng loạt nhãn vị trí ({locs.Count} nhãn, {copies} bản/nhãn)", cancellationToken);
        return locPdfBytes;
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
        var payload = $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}";
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

        // Remaining Weight
        detailsCell.Add(new Paragraph($"K.lượng: {(lot.RemainingWeightKg == 0 ? "0" : lot.RemainingWeightKg.ToString("N2"))} kg")
            .SetFontSize(4.5f)
            .SetMarginBottom(0.5f));

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
        var payload = $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}";
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
        // Payload format: STOCKLITE|{WarehouseId}|{EntityType}|{QrCode}
        // parts[0] = "STOCKLITE" (header)
        // parts[1] = WarehouseId (số nguyên — KHÔNG phải version)
        // parts[2] = EntityType (PADDY_LOT / LOCATION)
        // parts[3] = QrCode (unique code của entity)
        if (parts.Length != 4 || parts[0] != "STOCKLITE")
        {
            throw new ArgumentException("Payload QR không đúng định dạng STOCKLITE|{WarehouseId}|{EntityType}|{QrCode}.", nameof(request));
        }

        // Lấy WarehouseId từ parts[1] (để validate nếu cần, không dùng version check)
        if (!int.TryParse(parts[1], out var warehouseId) || warehouseId <= 0)
        {
            throw new ArgumentException("Payload QR không hợp lệ: WarehouseId phải là số nguyên dương.", nameof(request));
        }

        var entityType = parts[2].ToUpper();
        var qrCode = parts[3];

        if (entityType == "PADDY_LOT" || entityType == "BAG")
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
                EntityType = entityType,
                EntityId = lot.Id,
                QrCode = lot.QrCode,
                DisplayCode = lot.LotCode,
                LotType = lot.LotType,
                RemainingWeightKg = lot.RemainingWeightKg,
                IsQuarantined = isQuarantined,
                ProductVariant = lot.ProductVariant == null ? null : new QrProductVariantDto
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

        else if (entityType == "SKU")
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(x => x.SKU == qrCode, cancellationToken);

            if (variant == null)
            {
                throw new KeyNotFoundException("Không tìm thấy thông tin sản phẩm tương ứng với SKU trong mã QR.");
            }

            if (variant.IsDeleted)
            {
                throw new InvalidOperationException("Sản phẩm này đã bị xóa trên hệ thống.");
            }

            var response = new QrResolveResponseDto
            {
                EntityType = "SKU",
                EntityId = variant.Id,
                QrCode = variant.SKU,
                DisplayCode = variant.SKU,
                ProductVariant = new QrProductVariantDto
                {
                    Id = variant.Id,
                    Sku = variant.SKU,
                    Name = variant.Name
                },
                NavigationTarget = new QrNavigationTargetDto
                {
                    Type = "PRODUCT_VARIANT_DETAIL",
                    Id = variant.Id
                }
            };

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

    private async Task LogPrintAuditAsync(string targetType, string targetId, int copies, string description, CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor?.HttpContext;
        var userId = httpContext?.GetCurrentUserId();
        var ip = httpContext?.GetRemoteHostIpAddress();
        var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString();

        var audit = new AuditLog
        {
            Action = "PRINT_QR_LABEL",
            TargetType = targetType,
            TargetId = targetId,
            CreatedDate = DateTimeHelper.VietnamNow(),
            CreatedBy = userId,
            IpAddress = ip,
            UserAgent = userAgent,
            DataBefore = null,
            DataAfter = copies.ToString(),
            Description = description
        };
        await _context.AuditLogs.AddAsync(audit, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<QrLabelPreviewDto> GetQrLabelPreviewAsync(string? labelType, int? subjectId, string? template, CancellationToken cancellationToken = default)
    {
        var result = new QrLabelPreviewDto
        {
            Templates = new List<QrLabelTemplateInfoDto>
            {
                new() { Code = "SMALL", Name = "Nhãn cỡ nhỏ (50x30 mm)", WidthMm = 50f, HeightMm = 30f },
                new() { Code = "MEDIUM", Name = "Nhãn cỡ vừa (70x50 mm)", WidthMm = 70f, HeightMm = 50f },
                new() { Code = "LARGE", Name = "Nhãn cỡ lớn (100x70 mm)", WidthMm = 100f, HeightMm = 70f }
            },
            Formats = new List<string> { "PDF", "PNG" },
            LabelTypes = new List<string> { "PADDY_LOT", "LOCATION", "SKU", "BAG" }
        };

        if (string.IsNullOrEmpty(labelType) || subjectId == null || string.IsNullOrEmpty(template))
        {
            return result;
        }

        var validTemplate = result.Templates.FirstOrDefault(x => x.Code == template.ToUpper());
        if (validTemplate == null)
        {
            throw new ArgumentException("Kích thước nhãn không hợp lệ.");
        }

        var labelTypeUpper = labelType.ToUpper();
        LabelPreviewDataDto? previewData = null;

        if (labelTypeUpper == "PADDY_LOT")
        {
            var lot = await _context.PaddyLots
                .Include(x => x.ProductVariant)
                .Include(x => x.RiceVariety)
                .Include(x => x.Warehouse)
                .Include(x => x.Location)
                .FirstOrDefaultAsync(x => x.Id == subjectId && !x.IsDeleted, cancellationToken);
            
            if (lot != null)
            {
                previewData = new LabelPreviewDataDto
                {
                    LabelType = labelTypeUpper,
                    SubjectId = lot.Id,
                    Template = template.ToUpper(),
                    QrPayload = string.IsNullOrEmpty(lot.QrCode) ? "" : $"STOCKLITE|{lot.WarehouseId}|PADDY_LOT|{lot.QrCode}",
                    DisplayCode = lot.LotCode,
                    ProductName = lot.ProductVariant?.Name,
                    Sku = lot.ProductVariant?.SKU,
                    RiceVarietyName = lot.RiceVariety?.Name,
                    WeightKg = lot.RemainingWeightKg,
                    InboundDate = lot.InboundDate,
                    WarehouseName = lot.Warehouse?.Name,
                    LocationName = lot.Location == null ? null : FormatLocation(lot.Location),
                    IsQuarantined = lot.Status?.Code == LotStatusCodeConstants.Quarantine
                };
            }
        }
        else if (labelTypeUpper == "BAG")
        {
            var lot = await _context.PaddyLots
                .Include(x => x.ProductVariant)
                .Include(x => x.Warehouse)
                .FirstOrDefaultAsync(x => x.Id == subjectId && !x.IsDeleted, cancellationToken);
            
            if (lot != null)
            {
                previewData = new LabelPreviewDataDto
                {
                    LabelType = labelTypeUpper,
                    SubjectId = lot.Id,
                    Template = template.ToUpper(),
                    QrPayload = string.IsNullOrEmpty(lot.QrCode) ? "" : $"STOCKLITE|{lot.WarehouseId}|BAG|{lot.QrCode}",
                    DisplayCode = lot.LotCode,
                    ProductName = lot.ProductVariant?.Name,
                    Sku = lot.ProductVariant?.SKU,
                    PackageWeightKg = lot.ProductVariant?.Weight,
                    InboundDate = lot.InboundDate,
                    WarehouseName = lot.Warehouse?.Name,
                    IsQuarantined = lot.Status?.Code == LotStatusCodeConstants.Quarantine
                };
            }
        }
        else if (labelTypeUpper == "LOCATION")
        {
            var loc = await _context.Locations
                .Include(x => x.Warehouse)
                .FirstOrDefaultAsync(x => x.Id == subjectId && !x.IsDeleted, cancellationToken);
            
            if (loc != null)
            {
                previewData = new LabelPreviewDataDto
                {
                    LabelType = labelTypeUpper,
                    SubjectId = loc.Id,
                    Template = template.ToUpper(),
                    QrPayload = string.IsNullOrEmpty(loc.QrCode) ? "" : $"STOCKLITE|{loc.WarehouseId}|LOCATION|{loc.QrCode}",
                    DisplayCode = FormatLocation(loc),
                    WarehouseName = loc.Warehouse?.Name,
                    LocationName = FormatLocation(loc),
                    IsQuarantined = loc.IsQuarantine
                };
            }
        }
        else if (labelTypeUpper == "SKU")
        {
            var variant = await _context.ProductVariants
                .FirstOrDefaultAsync(x => x.Id == subjectId && !x.IsDeleted, cancellationToken);
            
            if (variant != null)
            {
                previewData = new LabelPreviewDataDto
                {
                    LabelType = labelTypeUpper,
                    SubjectId = variant.Id,
                    Template = template.ToUpper(),
                    QrPayload = string.IsNullOrEmpty(variant.SKU) ? "" : $"STOCKLITE|1|SKU|{variant.SKU}",
                    DisplayCode = variant.SKU,
                    ProductName = variant.Name,
                    Sku = variant.SKU,
                    PackageWeightKg = variant.Weight,
                };
            }
        }

        result.Label = previewData;
        return result;
    }

    public async Task<byte[]> GenerateBagLabelPdfAsync(int id, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var lot = await _context.PaddyLots
            .Include(x => x.ProductVariant)
            .Include(x => x.Warehouse)
            .FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, cancellationToken);

        if (lot == null)
        {
            throw new KeyNotFoundException($"Không tìm thấy lô hàng với ID {id} để in nhãn bao.");
        }

        if (string.IsNullOrWhiteSpace(lot.QrCode))
        {
            lot.QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper();
            await _context.SaveChangesAsync(cancellationToken);
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

                for (int i = 0; i < copies; i++)
                {
                    if (i > 0) pdf.AddNewPage();
                    AddBagLabelContent(doc, lot, widthPt, heightPt, font);
                }
                doc.Close();
            }
        }

        await LogPrintAuditAsync("Bag", id.ToString(), copies, $"In {copies} nhãn bao hàng của lô {lot.LotCode}", cancellationToken);

        return ms.ToArray();
    }

    public async Task<byte[]> GenerateBulkBagLabelsPdfAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        if (ids == null || !ids.Any())
        {
            throw new ArgumentException("Danh sách ID không được rỗng.", nameof(ids));
        }

        var distinctIds = ids.Distinct().ToList();
        var lots = await _context.PaddyLots
            .Include(x => x.ProductVariant)
            .Include(x => x.Warehouse)
            .Where(x => distinctIds.Contains(x.Id) && !x.IsDeleted).OrderBy(x => x.Id)
            .ToListAsync(cancellationToken);

        if (lots.Count != distinctIds.Count)
        {
            throw new KeyNotFoundException("Một hoặc nhiều lô hàng không tồn tại hoặc đã bị xóa.");
        }

        var lotsWithoutQr = lots.Where(x => string.IsNullOrWhiteSpace(x.QrCode)).ToList();
        if (lotsWithoutQr.Count > 0)
        {
            foreach (var lot in lotsWithoutQr)
                lot.QrCode = "PL-" + Guid.NewGuid().ToString("N").ToUpper();
            await _context.SaveChangesAsync(cancellationToken);
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
                        AddBagLabelContent(doc, lot, widthPt, heightPt, font);
                    }
                }
                doc.Close();
            }
        }

        await LogPrintAuditAsync("Bag", string.Join(",", distinctIds), copies, $"In hàng loạt nhãn bao hàng ({lots.Count} nhãn, {copies} bản/nhãn)", cancellationToken);

        return ms.ToArray();
    }

    public async Task<byte[]> GenerateBulkPaddyLotLabelsPngZipAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await GenerateBulkPaddyLotLabelsPdfAsync(ids, templateCode, copies, cancellationToken);
        var distinctIds = ids.Distinct().ToList();
        var lots = await _context.PaddyLots.Where(x => distinctIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        
        var prefixes = lots.Select(lot => $"LOT_{lot.LotCode}").ToList();

        return await ConvertPdfToPngZipAsync(pdfBytes, prefixes, copies, cancellationToken);
    }

    public async Task<byte[]> GenerateBulkLocationLabelsPngZipAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await GenerateBulkLocationLabelsPdfAsync(ids, templateCode, copies, cancellationToken);
        var distinctIds = ids.Distinct().ToList();
        var locs = await _context.Locations.Where(x => distinctIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        
        var prefixes = locs.Select(loc => $"LOC_{loc.Id}").ToList();

        return await ConvertPdfToPngZipAsync(pdfBytes, prefixes, copies, cancellationToken);
    }

    public async Task<byte[]> GenerateBulkBagLabelsPngZipAsync(List<int> ids, string templateCode, int copies, CancellationToken cancellationToken = default)
    {
        var pdfBytes = await GenerateBulkBagLabelsPdfAsync(ids, templateCode, copies, cancellationToken);
        var distinctIds = ids.Distinct().ToList();
        var lots = await _context.PaddyLots.Where(x => distinctIds.Contains(x.Id)).OrderBy(x => x.Id).ToListAsync(cancellationToken);
        
        var prefixes = lots.Select(lot => $"BAG_{lot.LotCode}").ToList();

        return await ConvertPdfToPngZipAsync(pdfBytes, prefixes, copies, cancellationToken);
    }

    private async Task<byte[]> ConvertPdfToPngZipAsync(byte[] pdfBytes, List<string> fileNamesPrefixes, int copies, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using var docReader = Docnet.Core.DocLib.Instance.GetDocReader(pdfBytes, new Docnet.Core.Models.PageDimensions(1));
            int pageCount = docReader.GetPageCount();
            
            for (int i = 0; i < pageCount; i++)
            {
                int prefixIndex = i / copies;
                int copyIndex = (i % copies) + 1;
                string prefix = prefixIndex < fileNamesPrefixes.Count ? fileNamesPrefixes[prefixIndex] : "LABEL";
                string fileName = copies > 1 ? $"{prefix}_{copyIndex:D3}.png" : $"{prefix}.png";
                
                using var pageReader = docReader.GetPageReader(i);
                var rawBytes = pageReader.GetImage(Docnet.Core.Models.RenderFlags.RenderAnnotations);
                int width = pageReader.GetPageWidth();
                int height = pageReader.GetPageHeight();

                var readSettings = new ImageMagick.MagickReadSettings 
                { 
                    Width = (uint)width, 
                    Height = (uint)height, 
                    Format = ImageMagick.MagickFormat.Bgra 
                };

                using var image = new ImageMagick.MagickImage(rawBytes, readSettings);
                var pngBytes = image.ToByteArray(ImageMagick.MagickFormat.Png);

                var entry = archive.CreateEntry(fileName);
                using var entryStream = entry.Open();
                await entryStream.WriteAsync(pngBytes, 0, pngBytes.Length, cancellationToken);
            }
        }
        return ms.ToArray();
    }

    private void AddBagLabelContent(Document doc, PaddyLot lot, float widthPt, float heightPt, PdfFont? font)
    {
        var headerTable = new Table(UnitValue.CreatePercentArray(new float[] { 100f }))
            .SetWidth(UnitValue.CreatePercentValue(100f));
        
        var titleParagraph = new Paragraph("BAO HÀNG")
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
        var payload = $"STOCKLITE|{lot.WarehouseId}|BAG|{lot.QrCode}";
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

        // SKU
        detailsCell.Add(new Paragraph($"SKU: {lot.ProductVariant?.SKU ?? "N/A"}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(0.5f));

        // Lot Code
        detailsCell.Add(new Paragraph($"Mã Lô: {lot.LotCode}")
            .SetFontSize(5f)
            .SetBold()
            .SetMarginBottom(0.5f));

        // Variant Name
        detailsCell.Add(new Paragraph($"Sản phẩm: {lot.ProductVariant?.Name ?? "N/A"}")
            .SetFontSize(4.5f)
            .SetMultipliedLeading(0.9f)
            .SetMarginBottom(0.5f));

        // Package Weight / UoM
        if (lot.ProductVariant != null)
        {
            var uom = lot.ProductVariant.UnitOfMeasure?.Name ?? "kg";
            var weightStr = lot.ProductVariant.Weight.ToString("N2");
            detailsCell.Add(new Paragraph($"Quy cách: {weightStr} {uom}")
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

        // Footer / Instruction
        detailsCell.Add(new Paragraph("Quét mã Bao hàng để truy xuất")
            .SetFontSize(4f)
            .SetFontColor(ColorConstants.GRAY)
            .SetItalic()
            .SetMarginBottom(0.5f));

        bodyTable.AddCell(detailsCell);
        doc.Add(bodyTable);
    }
}
