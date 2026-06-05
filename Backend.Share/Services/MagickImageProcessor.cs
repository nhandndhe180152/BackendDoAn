using System;
using Backend.Share.Enums;
using ImageMagick;
using static Backend.Share.Extensions.ImageProcessorExtentions;

namespace Backend.Share.Services;

public class MagickImageProcessor : IImageProcessor
{
    public async Task<byte[]> ResizeAsync(Stream imageStream, int width, int height)
    {
        using var image = await CreateImageAsync(imageStream);
        image.Resize((uint)width, (uint)height);
        return image.ToByteArray(GetMagickFormat(ImageFormat.Jpeg));
    }

    public async Task<byte[]> CropAsync(Stream imageStream, int x, int y, int width, int height)
    {
        using var image = await CreateImageAsync(imageStream);
        image.Crop(new MagickGeometry(x, y, (uint)width, (uint)height));
        ResetCanvas(image);
        return image.ToByteArray(GetMagickFormat(ImageFormat.Jpeg));
    }

    public async Task<byte[]> CropToSquareAsync(Stream imageStream)
    {
        using var image = await CreateImageAsync(imageStream);
        uint size = Math.Min(image.Width, image.Height);
        var x = (int)((image.Width - size) / 2);
        var y = (int)((image.Height - size) / 2);
        image.Crop(new MagickGeometry(x, y, size, size));
        ResetCanvas(image);
        return image.ToByteArray(GetMagickFormat(ImageFormat.Jpeg));
    }

    public async Task<byte[]> ResizeAndConvertAsync(Stream imageStream, int width, int height, ImageFormat format, int quality = 85)
    {
        using var image = await CreateImageAsync(imageStream);
        image.Resize((uint)width, (uint)height);
        image.Quality = (uint)quality;
        return image.ToByteArray(GetMagickFormat(format));
    }

    public async Task<byte[]> ConvertFormatAsync(Stream imageStream, ImageFormat format, int quality = 85)
    {
        using var image = await CreateImageAsync(imageStream);
        image.Quality = (uint)quality;
        return image.ToByteArray(GetMagickFormat(format));
    }

    public async Task<byte[]> OptimizeAsync(Stream imageStream, int maxWidth, ImageFormat format, int quality = 80)
    {
        using var image = await CreateImageAsync(imageStream);
        if (image.Width > maxWidth)
        {
            image.Resize((uint)maxWidth, 0); // maintain aspect ratio
        }
        image.Quality = (uint)quality;
        return image.ToByteArray(GetMagickFormat(format));
    }

    public async Task<ImageInfo> GetImageInfoAsync(Stream imageStream)
    {
        using var image = await CreateImageAsync(imageStream);
        return new ImageInfo
        {
            Width = (int)image.Width,
            Height = (int)image.Height,
            Format = TryParseFormat(image.Format)
        };
    }

    public string GetFileExtension(ImageFormat format) => format.ToString().ToLower();

    public async Task<byte[]> ProcessByProfileAsync(Stream imageStream, Enums.ImageProfile profile)
    {
        var (w, h, crop) = ImageProfilePresets.GetSize(profile);
        using var image = await CreateImageAsync(imageStream);

        if (crop)
        {
            image.Crop(new MagickGeometry(0, 0, (uint)w, (uint)h));
            ResetCanvas(image);
        }
        else
        {
            image.Resize((uint)w, (uint)h);
        }

        return image.ToByteArray(GetMagickFormat(ImageFormat.Webp));
    }

    private static async Task<MagickImage> CreateImageAsync(Stream imageStream)
    {
        if (imageStream.CanSeek)
            imageStream.Position = 0;

        using var memStream = new MemoryStream();
        await imageStream.CopyToAsync(memStream);
        return new MagickImage(memStream.ToArray());
    }

    private static MagickFormat GetMagickFormat(ImageFormat format) => format switch
    {
        ImageFormat.Jpeg => MagickFormat.Jpeg,
        ImageFormat.Png => MagickFormat.Png,
        ImageFormat.Webp => MagickFormat.WebP,
        ImageFormat.Gif => MagickFormat.Gif,
        _ => MagickFormat.Jpeg
    };

    private static ImageFormat TryParseFormat(MagickFormat format)
    {
        return Enum.TryParse<ImageFormat>(format.ToString(), true, out var result)
            ? result
            : ImageFormat.Jpeg;
    }

    private static void ResetCanvas(MagickImage image)
    {
        image.Page = new MagickGeometry(0, 0, image.Width, image.Height);
    }
}
