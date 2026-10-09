using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VL.Devices.Orbbec
{
    public static class SpanHelper
    {
        public unsafe static ReadOnlySpan<byte> CreateSpan(IntPtr pointer, uint length)
        {
            return new ReadOnlySpan<byte>(pointer.ToPointer(), (int)length);
        }
    }
}
