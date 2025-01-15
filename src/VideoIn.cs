using Microsoft.Extensions.Logging;
using System.ComponentModel;
using VL.Lib.Basics.Video;
using VL.Model;
using VL.Devices.Orbbec.Advanced;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Orbbec;
using System.Net;

namespace VL.Devices.Orbbec
{
    [ProcessNode]
    public class VideoIn : IVideoSource2, IDisposable
    {
        private readonly ILogger _logger;
        private readonly BehaviorSubject<Acquisition?> _aquicitionStarted = new BehaviorSubject<Acquisition?>(null);

        private int _changedTicket;
        private Advanced.DeviceInfo? _device;
        private Int2 _resolution;
        private int _fps;
        //private IConfiguration? _configuration;
        private bool _enabled;
        private string _IP;


        internal string Info { get; set; } = "";
        internal Spread<PropertyInfo> PropertyInfos { get; set; } = new SpreadBuilder<PropertyInfo>().ToSpread();

        public VideoIn([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
        }

        [return: Pin(Name = "Output")]
        public VideoIn? Update(
            OrbbecDevice? device,
            [DefaultValue("")] string IP,
            [DefaultValue("640, 576")] Int2 resolution,
            [DefaultValue("30")] int FPS,
            //IConfiguration configuration,
            [DefaultValue("true")] bool enabled,
            out string Info)
        {
            // By comparing the device info we can be sure that on re-connect of the device we see the change
            if (!Equals(device?.Tag, _device) || IP != _IP || enabled != _enabled || resolution != _resolution || FPS != _fps)// || configuration != _configuration)
            {
                _device = device?.Tag as Advanced.DeviceInfo;
                var ip_port = IP.Split(":");
                if (ip_port.Length == 2 && IPAddress.TryParse(ip_port[0], out var ip) && ushort.TryParse(ip_port[1], out var port))
                {
                    var dvc = ContextManager.GetHandle().Resource?.CreateNetDevice(ip.ToString(), port);
                    if (dvc != null)
                        _logger.LogInformation("Net device serial Nr: " + dvc.GetDeviceInfo().SerialNumber());
                    else
                        _logger.LogInformation("No net device found at: " + IP);
                }
                _IP = IP;
                _resolution = resolution;
                _fps = FPS;
                //_configuration = configuration;
                _enabled = enabled;
                _changedTicket++;
            }

            Info = this.Info;

            if (!enabled) return null;

            return this;
        }

        internal IObservable<Acquisition> AcquisitionStarted => _aquicitionStarted.Where(a => a != null && !a.IsDisposed)!;

        IVideoPlayer? IVideoSource2.Start(VideoPlaybackContext ctx)
        {
            var device = _device;
            if (device is null)
                return null;

            try
            {
                var result = Acquisition.Start(this, device, _logger, _resolution, _fps);//, _configuration
                //_aquicitionStarted.OnNext(result);
                return result;
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to start image acquisition");
                return null;
            }
        }

        int IVideoSource2.ChangedTicket => _changedTicket;

        public void Dispose()
        {
            //_ic4LibSubscription.Dispose();
        }
    }
}
