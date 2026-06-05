using System;
using Backend.Share.Enums;

namespace Backend.Share.Services;

public interface IImageProcessor
{
    // Resize ảnh về kích thước cố định
    Task<byte[]> ResizeAsync(Stream imageStream, int width, int height);

    // Crop ảnh tại vị trí chỉ định
    Task<byte[]> CropAsync(Stream imageStream, int x, int y, int width, int height);

    // Crop ảnh thành hình vuông (center)
    Task<byte[]> CropToSquareAsync(Stream imageStream);

    // Resize và chuyển định dạng ảnh
    Task<byte[]> ResizeAndConvertAsync(Stream imageStream, int width, int height, ImageFormat format, int quality = 85);

    // Chuyển định dạng ảnh (jpeg, png, webp...)
    Task<byte[]> ConvertFormatAsync(Stream imageStream, ImageFormat format, int quality = 85);

    // Tối ưu ảnh với max width và format
    Task<byte[]> OptimizeAsync(Stream imageStream, int maxWidth, ImageFormat format, int quality = 80);

    // Lấy thông tin cơ bản về ảnh
    Task<ImageInfo> GetImageInfoAsync(Stream imageStream);

    // Trả về phần mở rộng theo định dạng
    string GetFileExtension(ImageFormat format);

    // Resize và crop theo cấu hình profile
    Task<byte[]> ProcessByProfileAsync(Stream imageStream, ImageProfile profile);
}
