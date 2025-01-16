using Microsoft.Extensions.Logging;
using System.ComponentModel;
using VL.Lib.Basics.Video;
using VL.Model;
using VL.Devices.Orbbec.Advanced;
using System.Reactive.Subjects;
using System.Reactive.Linq;
using Orbbec;
using System.Net;
using VL.Lang.PublicAPI;

namespace VL.Devices.Orbbec
{
    [ProcessNode]
    public class NetDevice : IDisposable
    {
        private readonly ILogger _logger;
        private string _IP;
        private int _Port;
        private string _EnumEntry;

        public NetDevice([Pin(Visibility = PinVisibility.Hidden)] NodeContext nodeContext)
        {
            _logger = nodeContext.GetLogger();
        }

        public string Update(string IP, int Port = 8090)
        {
            if (IP != _IP || Port != _Port)
            {
                _IP = IP;
                _Port = Port;

                OrbbecDeviceDefinition.Instance.RemoveNetDevice(_IP, _Port);

                if (IPAddress.TryParse(_IP, out var ip) && _Port >= 0 && _Port <= ushort.MaxValue)
                    _EnumEntry = OrbbecDeviceDefinition.Instance.AddNetDevice(_IP, _Port, _logger);
                else
                {
                    _EnumEntry = "";
                    _logger.LogError("Not a valid IP:Port " + IP + ":" + Port);
                }
            }

            return _EnumEntry;
        }
        public void Dispose()
        {
            OrbbecDeviceDefinition.Instance.RemoveNetDevice(_IP, _Port);
        }
    }
}
