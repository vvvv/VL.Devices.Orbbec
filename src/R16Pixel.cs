#nullable enable
using VL.Lib.Basics.Imaging;
using VL.Lib.Basics.Video;

namespace VL.Devices.Orbbec
{
    public record struct R16Pixel(ushort R) : IPixel
    {
        public PixelFormat PixelFormat => PixelFormat.R16;
    }
}
#nullable restore