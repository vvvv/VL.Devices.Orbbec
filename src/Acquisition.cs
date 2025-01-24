using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using VL.Lib.Basics.Resources;
using VL.Lib.Basics.Video;
using OurLogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Text;
using VL.Devices.Orbbec.Advanced;
using Orbbec;
using System;
using static VL.Core.AppHost;
using VL.Core;
using System.Collections;
using System.Runtime.InteropServices;
using System.Reflection;
using Frame = Orbbec.Frame;
using System.Buffers;
using Point = Orbbec.Point;

namespace VL.Devices.Orbbec
{
    internal class Acquisition : IVideoPlayer
    {
        public static Acquisition? Start(VideoIn videoIn, Device device, ILogger logger, Int2 resolution, int fps)//, IConfiguration? configuration)
        {
            var deviceInfo = device.GetDeviceInfo();
            logger.Log(OurLogLevel.Information, "Starting image acquisition on {device}", deviceInfo.SerialNumber());

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
                StreamProfile colorProfile = pipe.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(0, 0, Format.OB_FORMAT_BGRA, fps);
                StreamProfile depthProfile = pipe.GetStreamProfileList(SensorType.OB_SENSOR_DEPTH).GetVideoStreamProfile(resolution.X, resolution.Y, Format.OB_FORMAT_Y16, fps);
                
                Config config = new Config();
                
                config.EnableStream(depthProfile);
                config.EnableStream(colorProfile);
                //config.SetFrameAggregateOutputMode(FrameAggregateOutputMode.OB_FRAME_AGGREGATE_OUTPUT_ALL_TYPE_FRAME_REQUIRE);

                //pipe.EnableFrameSync();
                pipe.Start(config);

                logger.Log(OurLogLevel.Information, $"Stream started for device {deviceInfo.SerialNumber()}");
            }
            catch (Exception ex)
            {
                logger.Log(OurLogLevel.Error, $"Error starting stream for device {deviceInfo.SerialNumber()}: {ex.Message}");
                return null;
            }

            var pointCloud = new PointCloudFilter();
            pointCloud.SetCreatePointFormat(Format.OB_FORMAT_POINT);

            var align = new AlignFilter(StreamType.OB_STREAM_DEPTH);

            var contextHandle = ContextManager.GetHandle();

            return new Acquisition(contextHandle, logger, pipe, resolution, videoIn, pointCloud, align);
        }

        private IResourceHandle<Context> _contextHandle;
        private IResourceProvider<Frameset> _frames;
        private readonly ILogger _logger;
        private readonly Pipeline _pipeline;
        private readonly Int2 _resolution;
        private PointCloudFilter _pointCloud;
        private AlignFilter _align;

        public Acquisition(IResourceHandle<Context> contextHandle, ILogger logger, Pipeline pipeline, Int2 resolution, VideoIn videoIn, PointCloudFilter pointCloud, AlignFilter alignFilter)//, int fps)
        {
            _contextHandle = contextHandle;
            _logger = logger;
            _pipeline = pipeline;
            _resolution = resolution;
            _pointCloud = pointCloud;
            _align = alignFilter;

            _frames = ResourceProvider.New(() => _pipeline.WaitForFrames(1000))
                .ShareInParallel();
        }

        public bool IsDisposed { get; private set; }

        public void Dispose()
        {
            if (IsDisposed)
                return;

            IsDisposed = true;

            _logger.Log(OurLogLevel.Information, "Stopping image acquisition");

            try
            {
                _pipeline.Stop();
                //_pipeline.GetDevice().Dispose();
                _pipeline.Dispose();
                _pointCloud.Dispose();
                _align.Dispose();
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

        private static IMemoryOwner<Rgba32fPixel> PiontsToRGBA32(Span<Point> pointsData)
        {
            var memoryOwner = MemoryPool<Rgba32fPixel>.Shared.Rent(pointsData.Length);
            var colorData = memoryOwner.Memory.Span;
            
            for (int i = 0; i < pointsData.Length; i ++)
            {
                var point = pointsData[i];
                colorData[i] = new Rgba32fPixel(point.x, point.y, point.z, 1f);
            }
            return memoryOwner;
        }

        private static IMemoryOwner<Rgba32fPixel> ColorPiontsToRGBA32(Span<ColorPoint> pointsData)
        {
            var memoryOwner = MemoryPool<Rgba32fPixel>.Shared.Rent(pointsData.Length);
            var colorData = memoryOwner.Memory.Span;

            for (int i = 0; i < pointsData.Length; i++)
            {
                var point = pointsData[i];
                colorData[i] = new Rgba32fPixel(point.x, point.y, point.z, 1f);
            }
            return memoryOwner;
        }

        public unsafe IResourceProvider<Lib.Basics.Video.VideoFrame>? GrabVideoFrame()
        {
           /* return _frames.Bind(frameset =>
            {
                if (frameset is null)
                    return null;

                var depthFrame = frameset.GetColorFrame();
                var width = depthFrame.GetWidth();
                var height = depthFrame.GetHeight();
                var stride = depthFrame.GetDataSize();

                var format = depthFrame.GetFormat();

                var memoryOwner = new UnmanagedMemoryManager<BgraPixel>(depthFrame.GetDataPtr(), (int)depthFrame.GetDataSize());

                var pitch = width * sizeof(BgraPixel);
                var memory = memoryOwner.Memory.AsMemory2D(0, (int)height, (int)width, 0);
                var videoFrame = new VideoFrame<BgraPixel>(memory);
                return ResourceProvider.Return(videoFrame, memoryOwner,
                    static x =>
                    {
                        ((IDisposable)x).Dispose();
                    });
            });*/

            Frameset frames = _pipeline.WaitForFrames(1);

            if (frames == null)
                return null;

            //var depthFrame = frames.GetColorFrame();
            var depthFrame = frames.GetDepthFrame();

            using Frame alignedFrameset = _align.Process(frames);
            using Frame frame = _pointCloud.Process(alignedFrameset);

            if (frame == null || depthFrame == null)
            {
                frames.Dispose();
                return null;
            }

            using PointsFrame pointsFrame = frame.As<PointsFrame>();

            var width = (int)depthFrame.GetWidth();
            var height = (int)depthFrame.GetHeight();

            var stride = pointsFrame.GetDataSize();
            var format = pointsFrame.GetFormat();

            var pointsData = new Span<Point>((void*)pointsFrame.GetDataPtr(), width * height);
            //var pointsRgbData = new Span<ColorPoint>((void*)depthFrame.GetDataPtr(), width * height);

            var memoryOwner = PiontsToRGBA32(pointsData); //new UnmanagedMemoryManager<Rgba32fPixel>(depthFrame.GetDataPtr(), (int)stride);

            //var pitch = width * sizeof(R16Pixel);
            var memory = memoryOwner.Memory.AsMemory2D(0, (int)height, (int)width, 0);
            var videoFrame = new VideoFrame<Rgba32fPixel>(memory);
            return ResourceProvider.Return(videoFrame, (memoryOwner, frames),
                static x =>
                {
                    ((IDisposable)x.memoryOwner).Dispose();
                    x.frames.Dispose();
                });
        }
    }
}
