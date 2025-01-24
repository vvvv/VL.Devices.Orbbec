#nullable enable
using VL.Lib.Basics.Imaging;
using VL.Lib.Basics.Video;

namespace VL.Devices.Orbbec
{
    public record struct Rgba32fPixel(float R, float G, float B, float A) : IPixel
    {
        public PixelFormat PixelFormat => PixelFormat.R32G32B32A32F;
    }
}
#nullable restore