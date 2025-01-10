using CommunityToolkit.HighPerformance;
using Microsoft.Extensions.Logging;
using VL.Lib.Basics.Resources;
using VL.Lib.Basics.Video;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;
using System.Text;
using VL.Devices.Orbbec.Advanced;
using Orbbec;

namespace VL.Devices.Orbbec
{
    internal class Acquisition : IVideoPlayer
    {
        public static Acquisition? Start(VideoIn videoIn, Advanced.DeviceInfo deviceInfo, ILogger logger, Int2 resolution, int fps)//, IConfiguration? configuration)
        {
            logger.Log(LogLevel.Information, "Starting image acquisition on {device}", deviceInfo.SerialNumber);

            var contextHandle = ContextManager.GetHandle().DisposeBy(AppHost.Global);

            Device device = contextHandle.Resource.QueryDeviceList().GetDevice(deviceInfo.Index);

            Pipeline pipe = new Pipeline(device);

            try
            {
                
                //StreamProfile colorProfile = VideoStreamProfile.Create(StreamType.OB_STREAM_COLOR, Format.OB_FORMAT_BGRA, (uint)resolution.X, (uint)resolution.Y, (uint)fps);//pipe.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(0, 0, Format.OB_FORMAT_RGB, 0);
                StreamProfile colorProfile = pipe.GetStreamProfileList(SensorType.OB_SENSOR_COLOR).GetVideoStreamProfile(0, 0, Format.OB_FORMAT_RGB, 0);
                Config config = new Config();
                //config.EnableVideoStream(SensorType.OB_SENSOR_COLOR, 0, 0, 0, Format.OB_FORMAT_BGRA);
                config.EnableStream(colorProfile);
                //config.EnableVideoStream(StreamType.OB_STREAM_DEPTH);

                pipe.Start(config);

                logger.Log(LogLevel.Information, $"Stream started for device {deviceInfo.SerialNumber}");
            }
            catch (Exception ex)
            {
                logger.Log(LogLevel.Error, $"Error starting stream for device {deviceInfo.SerialNumber}: {ex.Message}");
                return null;
            }
            /*
            var grabber = new Grabber();
            grabber.DeviceOpen(deviceInfo);

            var pMap = grabber.DevicePropertyMap;

            if (grabber.IsDeviceOpen)
            {
                logger.Log(LogLevel.Information, "Opened device {device}", grabber.DeviceInfo.ModelName);
            }
            else
            {
                logger.LogError("Failed to open device");
                return null;
            }
            
            // Set the frame rate and resolution
            var frameRate = pMap.Find(ic4.PropId.AcquisitionFrameRate);
            frameRate.TrySetValue(Math.Max(Math.Min(fps, (float)frameRate.Maximum), (float)frameRate.Minimum));
            
            var width = pMap.Find(ic4.PropId.Width);
            width.TrySetValue(Math.Max(Math.Min(resolution.X, width.Maximum), width.Minimum));

            var height = pMap.Find(ic4.PropId.Height);
            height.TrySetValue(Math.Max(Math.Min(resolution.Y, height.Maximum), height.Minimum));

            //apply static parameters
            configuration?.Configure(pMap);

            // Create a SnapSink. InMemoryConfiguration SnapSink allows grabbing single images (or image sequences) out of a data stream.
            var sink = new SnapSink(acceptedPixelFormat: PixelFormat.BGRa8);

            // Setup data stream from the video capture device to the sink and start image acquisition.
            grabber.StreamSetup(sink, ic4.StreamSetupOption.AcquisitionStart);

            //collect available properties
            var spb = new SpreadBuilder<PropertyInfo>();
            CollectPropertiesInfos(spb, pMap);
            videoIn.PropertyInfos = spb.ToSpread();
            
            videoIn.Info = $"Framerate range: [{frameRate.Minimum}, {frameRate.Maximum}], current FPS: {pMap.GetValueString(ic4.PropId.AcquisitionFrameRate)}" +
                           $"\r\nWidth range: [{width.Minimum}, {width.Maximum}], current Width {pMap.GetValueString(ic4.PropId.Width)}" +
                           $"\r\nHeight range: [{height.Minimum}, {height.Maximum}], current Height {pMap.GetValueString(ic4.PropId.Height)}" +
                           $"\r\n";
            /*
            //debug properites list
            string props = "";
            var allProps = pMap.All;
            foreach (var prop in allProps)
            {
                if (prop.IsAvailable)
                    if (prop.Type != PropertyType.Command && prop.Type != PropertyType.Register)
                    {
                        props += $"\r\n{prop.Name} ({prop.Type}) Description: {prop.Description}";
                    }
            }
            videoIn.Info += props + $"\r\n";

            //debug properties tree
            Property r = pMap.FindCategory("Root");
            var sb = new StringBuilder();
            TraverseCategories(sb, r, "");
            videoIn.Info += $"\r\n" + sb.ToString();
            */

            return new Acquisition(contextHandle, logger, pipe, resolution, videoIn);
        }

        /*
        static void CollectPropertiesInfos(SpreadBuilder<PropertyInfo> spb, PropertyMap propertyMap)
        {
            var props = propertyMap.All
                .Where(x => x.IsAvailable)
                .Where(x => x.Type != PropertyType.Command)
                .Where(x => x.Type != PropertyType.Register);
            foreach (var p in props)
            {
                switch (p.Type)
                {
                    case PropertyType.Float:
                        if (p is PropFloat f) 
                        {
                            spb.Add(new PropertyInfo(f.Name, f.Value, f.Description, f.Minimum, f.Maximum, Spread<string>.Empty, p.Type.ToString(), p.IsReadonly, p.IsLocked)); 
                        }
                        break;
                    case PropertyType.Integer:
                        if (p is PropInteger i) 
                        {
                            spb.Add(new PropertyInfo(i.Name, i.Value, i.Description, i.Minimum, i.Maximum, Spread<string>.Empty, i.Type.ToString(), i.IsReadonly, i.IsLocked)); 
                        }
                        break;
                    case PropertyType.Boolean:
                        if (p is PropBoolean b) 
                        {
                            spb.Add(new PropertyInfo(b.Name, b.Value, b.Description, false, true, Spread<string>.Empty, b.Type.ToString(), b.IsReadonly, b.IsLocked)); 
                        }
                        break;
                    case PropertyType.String:
                        if (p is PropString s) 
                        {
                            spb.Add(new PropertyInfo(s.Name, s.Value, s.Description, "", s.MaxLength, Spread<string>.Empty, s.Type.ToString(), s.IsReadonly, s.IsLocked)); 
                        }
                        break;
                    case PropertyType.Enumeration:
                        if (p is PropEnumeration e)
                        {
                            spb.Add(new PropertyInfo(e.Name, e.SelectedEntry.Name, e.Description, "", "", e.Entries.Select(x => x.Name).ToSpread(), e.Type.ToString(), e.IsReadonly, e.IsLocked));
                        }
                        break;
                    default:
                        // cannot set value
                        break;
                }
            }
        }
        
        static void TraverseCategories(StringBuilder sb, Property p, string offset)
        {
            if (p is PropCategory c)
            {
                sb.AppendLine($"{offset}--{ c.Name} ({ c.Type}) Description: { c.Description}");
                foreach (var cp in c.Features)
                {
                    if (cp.IsAvailable)
                    {
                        if (cp.Type != PropertyType.Category && cp.Type != PropertyType.Register)
                        {
                            sb.AppendLine($"{offset}    {cp.Name} ({cp.Type}) Description: {cp.Description}");
                        }
                        else
                        {
                            TraverseCategories(sb, cp, offset + "    ");
                        }
                    }
                }
            }
            else
            {
                sb.AppendLine($"\r\n{offset}{p.Name} ({p.Type}) Description: {p.Description}");
            }
            return;
        }*/

        private IResourceHandle<Context> _contextHandle;
        private readonly ILogger _logger;
        private readonly Pipeline _pipeline;
        private readonly Int2 _resolution;

        public Acquisition(IResourceHandle<Context> contextHandle, ILogger logger, Pipeline pipeline, Int2 resolution, VideoIn videoIn)//, int fps)
        {
            _contextHandle = contextHandle;
            _logger = logger;
            _pipeline = pipeline;
            _resolution = resolution; 
        }

        public bool IsDisposed { get; private set; }

        //public PropertyMap PropertyMap => _grabber.DevicePropertyMap;

        //public PixelFormat PixelFormat { get; set; } = new PixelFormat(PixelFormatName.BGRa8);

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

        public unsafe IResourceProvider<Lib.Basics.Video.VideoFrame>? GrabVideoFrame()
        {
            Frameset frames = _pipeline.WaitForFrames(100);
            
            var colorFrame = frames?.GetColorFrame();

            if (colorFrame == null)
            {
                return null;
            }

            //colorFrame.

            //var image = _sink.SnapSingle(TimeSpan.FromSeconds(1.5)); //should be long enough for the lowest frame rate

            var width = colorFrame.GetWidth();
            var height = colorFrame.GetHeight();
            var stride = colorFrame.GetDataSize();

            var format = colorFrame.GetFormat();

            var memoryOwner = new UnmanagedMemoryManager<BgraPixel>(colorFrame.GetDataPtr(), (int)colorFrame.GetDataSize());

            var pitch = width * sizeof(BgraPixel);
            var memory = memoryOwner.Memory.AsMemory2D(0, (int)height, (int)width, (int)pitch);
            var videoFrame = new VideoFrame<BgraPixel>(memory);
            return ResourceProvider.Return(videoFrame, (memoryOwner, colorFrame),
                static x =>
                {
                    ((IDisposable)x.memoryOwner).Dispose();
                    x.colorFrame.Dispose();
                });
        }
    }
}
