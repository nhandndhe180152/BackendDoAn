using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Backend.Application.DTOs.ProductVariants;
using Backend.Application.Interfaces;
using Backend.Domain.Entities;
using Backend.Domain.Interfaces.Repositories;
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

    public QRCodeService(
        IProductVariantRepository productVariantRepository, 
        IStorageService storageService,
        IInventoryRepository inventoryRepository)
    {
        _productVariantRepository = productVariantRepository;
        _storageService = storageService;
        _inventoryRepository = inventoryRepository;
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

                    for (int i = 0; i < item.Quantity; i++)
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
}
