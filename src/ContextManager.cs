using Orbbec;
using VL.Lib.Basics.Resources;

namespace VL.Devices.Orbbec
{
    internal static class ContextManager
    {
        private static IResourceProvider<Context> s_contextProvider = ResourceProvider.New(() => new Context())
            .Publish()
            .ShareInParallel();

        public static IResourceHandle<Context> GetHandle()
        {
            return s_contextProvider.GetHandle();
        }
    }
}
