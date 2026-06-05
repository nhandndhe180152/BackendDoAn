using System.Threading.Tasks;
using Backend.Application.DTOs.ProductVariants;

namespace Backend.Application.Interfaces;

public interface IQRCodeService
{
    /// <summary>
    /// Generates a raw QR Code PNG image for a product variant SKU.
    /// </summary>
    /// <param name="productVariantId">The ID of the product variant.</param>
    /// <returns>PNG image byte array.</returns>
    Task<byte[]> GenerateQRCodeImageAsync(int productVariantId);

    /// <summary>
    /// Generates a printable PDF label containing the QR code and product variant details.
    /// </summary>
    /// <param name="productVariantId">The ID of the product variant.</param>
    /// <param name="widthMm">The width of the label in millimeters.</param>
    /// <param name="heightMm">The height of the label in millimeters.</param>
    /// <returns>PDF byte array.</returns>
    Task<byte[]> GenerateQRLabelPdfAsync(int productVariantId, float widthMm = 50f, float heightMm = 30f);

    /// <summary>
    /// Generates a bulk PDF containing multiple labels for the specified variants and quantities.
    /// </summary>
    /// <param name="request">Request details including items, quantities, and dimensions.</param>
    /// <returns>PDF byte array.</returns>
    Task<byte[]> GenerateBulkQRLabelsPdfAsync(BatchQRLabelRequestDto request);

    /// <summary>
    /// Generates a QR code for a variant, uploads it to Cloudinary, and saves the URL in the database.
    /// </summary>
    /// <param name="productVariantId">The ID of the product variant.</param>
    /// <returns>The uploaded QR Code URL.</returns>
    Task<string> GenerateAndSaveQRUrlAsync(int productVariantId);

    /// <summary>
    /// Syncs and generates QR Code URLs for all product variants that do not have one.
    /// </summary>
    /// <returns>The count of synced variants.</returns>
    Task<int> SyncAllQRCodeUrlsAsync();
}
