using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using VL.Lib.Basics.Resources;
using VL.Lib.Basics.Video;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Text;
using VL.Devices.Orbbec.Advanced;
using Orbbec;
using System;
using static VL.Core.AppHost;
using VL.Core;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;

namespace VL.Devices.Orbbec
{
    internal class Acquisition : IVideoPlayer
    {
        public static Acquisition? Start(VideoIn videoIn, Advanced.DeviceInfo deviceInfo, ILogger logger, Int2 resolution, int fps)//, IConfiguration? configuration)
        {
            logger.Log(LogLevel.Information, "Starting image acquisition on {device}", deviceInfo.SerialNumber);

            var contextHandle = ContextManager.GetHandle();

            Device device = contextHandle.Resource.QueryDeviceList().GetDeviceBySN(deviceInfo.SerialNumber);
            Pipeline pipe = new Pipeline(device);

            /*SensorList s = device.GetSensorList();
            for (int i = 0; i < s.SensorCount(); i++)
            {
                logger.Log(LogLevel.Information, s.GetSensor((uint)i).GetSensorType().ToString());
            }

            StreamProfileList profilesColor = device.GetSensor(SensorType.OB_SENSOR_COLOR).GetStreamProfileList();
            for (int i = 0; i < profilesColor.ProfileCount(); i++)
            {
                var pC = profilesColor.GetProfile(i).As<VideoStreamProfile>();

                if (pC != null && pC.GetFormat() == Format.OB_FORMAT_BGRA)
                    logger.Log(LogLevel.Information, "Color: " + pC.GetWidth().ToString() + "x" + pC.GetHeight().ToString() + " FPS: " + pC.GetFPS().ToString());
            }*/
            videoIn.Info = "Supported formats:";

            StreamProfileList profilesDepth = pipe.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH);// device.GetSensor(SensorType.OB_SENSOR_DEPTH).GetStreamProfileList();
            for (int i = 0; i < profilesDepth.ProfileCount(); i++)
            {
                var pD = profilesDepth.GetProfile(i).As<VideoStreamProfile>();

                if (pD != null)// && pD.GetFormat() == Format.OB_FORMAT_RGB_POINT)
                    videoIn.Info += $"\r\nDepth resolution: " + pD.GetWidth().ToString() + "x" + pD.GetHeight().ToString() + " FPS: " + pD.GetFPS().ToString();
                    //logger.Log(LogLevel.Information, "Depth resolution: " + pD.GetWidth().ToString() + "x" + pD.GetHeight().ToString() + " FPS: " + pD.GetFPS().ToString());// + " Format: " + pD.GetFormat().ToString());
            }

            try
            {
                //StreamProfile colorProfile = pipe.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(resolution.X, resolution.Y, Format.OB_FORMAT_BGRA, fps);
                StreamProfile depthProfile = pipe.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH).GetVideoStreamProfile(resolution.X, resolution.Y, Format.OB_FORMAT_Y16, fps);
                Config config = new Config();
                config.EnableStream(depthProfile);

                pipe.Start(config);

                logger.Log(LogLevel.Information, $"Stream started for device {deviceInfo.SerialNumber}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Error starting stream for device {deviceInfo.SerialNumber}: {ex.Message}");
                return null;
            }

            return new Acquisition(contextHandle, logger, pipe, resolution, videoIn);
        }

        private IResourceHandle<Context> _contextHandle;
        private IResourceProvider<Frameset> _frames;
        private readonly ILogger _logger;
        private readonly Pipeline _pipeline;
        private readonly Int2 _resolution;

        public Acquisition(IResourceHandle<Context> contextHandle, ILogger logger, Pipeline pipeline, Int2 resolution, VideoIn videoIn)//, int fps)
        {
            _contextHandle = contextHandle;
            _logger = logger;
            _pipeline = pipeline;
            _resolution = resolution;

            _frames = ResourceProvider.New(() => _pipeline.WaitForFrames(1000))
                .ShareInParallel();
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            _logger.Log(LogLevel.Information, "Stopping image acquisition");

            try
            {
                _pipeline.Stop();
                //_pipeline.GetDevice().Dispose();
                _pipeline.Dispose();

                _contextHandle.Dispose();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Unexpected exception while stopping remote acquisition");
            }
        }
        private static byte[] ConvertDepthToRGBData(byte[] depthData)
        {
            byte[] colorData = new byte[depthData.Length / 2 * 4];
            for (int i = 0; i < depthData.Length; i += 2)
            {
                ushort depthValue = (ushort)((depthData[i + 1] << 8) | depthData[i]);
                float depth = (float)depthValue / 1000;
                byte depthByte = (byte)(depth * 255);
                int index = i / 2 * 4;
                colorData[index] = depthByte; // Red
                colorData[index + 1] = depthByte; // Green
                colorData[index + 2] = depthByte; // Blue
                colorData[index + 3] = 255;
            }
            return colorData;
        }

        public unsafe IResourceProvider<Lib.Basics.Video.VideoFrame>? GrabVideoFrame()
        {
           /* return _frames.Bind(frameset =>
            {
                if (frameset is null)
                    return null;

                var colorFrame = frameset.GetColorFrame();
                var width = colorFrame.GetWidth();
                var height = colorFrame.GetHeight();
                var stride = colorFrame.GetDataSize();

                var format = colorFrame.GetFormat();

                var memoryOwner = new UnmanagedMemoryManager<BgraPixel>(colorFrame.GetDataPtr(), (int)colorFrame.GetDataSize());

                var pitch = width * sizeof(BgraPixel);
                var memory = memoryOwner.Memory.AsMemory2D(0, (int)height, (int)width, 0);
                var videoFrame = new VideoFrame<BgraPixel>(memory);
                return ResourceProvider.Return(videoFrame, memoryOwner,
                    static x =>
                    {
                        ((IDisposable)x).Dispose();
                    });
            });*/

            Frameset frames = _pipeline.WaitForFrames(100);

            if (frames == null)
                return null;
            
            //var colorFrame = frames.GetColorFrame();
            var colorFrame = frames.GetDepthFrame();
            if (colorFrame == null)
            {
                frames.Dispose();
                return null;
            }

            var width = colorFrame.GetWidth();
            var height = colorFrame.GetHeight();
            var stride = colorFrame.GetDataSize();

            var format = colorFrame.GetFormat();

            byte[] data = new byte[colorFrame.GetDataSize()];
            colorFrame.CopyData(ref data);
            data = ConvertDepthToRGBData(data);

            GCHandle pinnedArray = GCHandle.Alloc(data, GCHandleType.Pinned);
            IntPtr pointer = pinnedArray.AddrOfPinnedObject();

            var memoryOwner = new UnmanagedMemoryManager<BgraPixel>(pointer, (int)colorFrame.GetDataSize() * 2);

            var pitch = width * sizeof(BgraPixel);
            var memory = memoryOwner.Memory.AsMemory2D(0, (int)height, (int)width, 0);
            var videoFrame = new VideoFrame<BgraPixel>(memory);
            return ResourceProvider.Return(videoFrame, (memoryOwner, frames, pinnedArray),
                static x =>
                {
                    ((IDisposable)x.memoryOwner).Dispose();
                    x.frames.Dispose();
                    x.pinnedArray.Free();
                });
        }
    }
}
