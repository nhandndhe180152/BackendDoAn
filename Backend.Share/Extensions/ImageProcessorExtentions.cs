using System;
using Backend.Share.Enums;

namespace Backend.Share.Extensions;

public static class ImageProcessorExtentions
{
    public static class ImageProfilePresets
    {
        public static (int Width, int Height, bool CropSquare) GetSize(ImageProfile profile) => profile switch
        {
            ImageProfile.Avatar => (150, 150, true),
            ImageProfile.BlogThumbnail => (300, 200, false),
            ImageProfile.BlogHeader => (1280, 720, false),
            ImageProfile.ProductSmall => (400, 400, false),
            ImageProfile.ProductLarge => (1024, 1024, false),
            _ => (0, 0, false)
        };
    }
}
