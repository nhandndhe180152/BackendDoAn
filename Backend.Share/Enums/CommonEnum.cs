using System;

namespace Backend.Share.Enums;

public enum Gender
{
    Male = 0,
    Female = 1,
    Other = 2
}

public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Rejected = 2
}

public enum ImageFormat
{
    Jpeg,
    Png,
    Webp,
    Gif
}
public class ImageInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public ImageFormat Format { get; set; }
}

public enum ImageProfile
{
    Avatar,         // 150x150 square
    BlogThumbnail,  // 300x200
    BlogHeader,     // 1280x720
    ProductSmall,   // 400x400
    ProductLarge,   // 1024x1024
    Original        // no resize, just convert
}
