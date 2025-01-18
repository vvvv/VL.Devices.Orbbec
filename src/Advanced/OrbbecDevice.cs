using System.Reactive.Linq;
using VL.Core.CompilerServices;
using Orbbec;
using System.Reactive.Subjects;
using VL.Lib.Basics.Resources;
using Microsoft.Extensions.Logging;
using System.Reflection;

namespace VL.Devices.Orbbec.Advanced;

internal record DeviceInfo(string SerialNumber);

[Serializable]
public class OrbbecDevice : DynamicEnumBase<OrbbecDevice, OrbbecDeviceDefinition>
{
    public OrbbecDevice(string value) : base(value)
    {
        var assemblyPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        var extensionsPath = Path.Combine(assemblyPath, @"..\..\runtimes\win-x64\native\extensions");
        Context.SetExtensionsDirectory(extensionsPath);
    }

    [CreateDefault]
    public static OrbbecDevice CreateDefault()
    {
        return CreateDefaultBase();
    }
}

public class OrbbecDeviceDefinition : DynamicEnumDefinitionBase<OrbbecDeviceDefinition>
{
    private readonly Subject<object> _devicesChanged = new();
    private IResourceHandle<Context?>? _context;
    private Dictionary<string, Device> _netDevices = new Dictionary<string, Device>();

    protected override void Initialize()
    {
        try
        {
            _context = ContextManager.GetHandle().DisposeBy(AppHost.Global);
            
            _context.Resource?.SetDeviceChangedCallback((removedList, addedList) =>
            {
                _devicesChanged.OnNext(this);
            });
        }
        catch
        {

        }

        base.Initialize();
    }

    private string NameFromDevice(Device device)
    {
        if (device != null)
        {
            var dvcInfo = device.GetDeviceInfo();
            return dvcInfo.Name() + " SN:" + dvcInfo.SerialNumber() + " " + dvcInfo.ConnectionType();
        }
        else
            return "NULL device";
    }

    private string SerialFromDevice(Device device)
    {
        return device?.GetDeviceInfo().SerialNumber() ?? "NULL device";
    }

    public string AddNetDevice(string ip, int port, ILogger logger)
    {
        try
        {
            var netDevice = ContextManager.GetHandle().Resource?.CreateNetDevice(ip, (ushort) port);
            if (netDevice != null)
            {
                _netDevices.Add(ip + ":" + port.ToString(), netDevice);
                _devicesChanged.OnNext(this);
                return NameFromDevice(netDevice);
            }
        }
        catch ( Exception )
        {
            logger.LogInformation("No Orbbec Net device found at: " + ip + ":" + port.ToString());
        }
        return "";
    }

    public void RemoveNetDevice(string ip, int port)
    {
        _netDevices.Remove(ip + ":" + port.ToString());
        _devicesChanged.OnNext(this);
    }

    //Return the current enum entries
    protected override IReadOnlyDictionary<string, object> GetEntries()
    {
        if (_context?.Resource is null)
        {
            return new Dictionary<string, object>()
            {
                { "Default", null! }
            };
        }
        _context.Resource.EnableNetDeviceEnumeration(true);
        DeviceList devices = _context.Resource.QueryDeviceList();

        var result = new Dictionary<string, object?>()
        {
            { "Default", _netDevices.Any() ? new DeviceInfo(SerialFromDevice(_netDevices.FirstOrDefault().Value)) : devices.DeviceCount() > 0 ? new DeviceInfo(SerialFromDevice(devices.GetDevice(0))) : null }
            //{ "Default", devices.DeviceCount() > 0 ? devices.GetDevice(0) : null }
        };

        //add net devices
        foreach (var entry in _netDevices)
        {
            var name = NameFromDevice(entry.Value);
            if (!result.ContainsKey(name))
            {
                result.Add(name, new DeviceInfo(SerialFromDevice(entry.Value)));
            }
        }

        //add usb devices
        for (uint i = 0; i < devices.DeviceCount(); i++)
        {
            var dvc = devices.GetDevice(i);
            var name = NameFromDevice(dvc);
            if (!result.ContainsKey(name))
            {
                result.Add(name, new DeviceInfo(SerialFromDevice(dvc)));
            }
        }

        return result!;
    }

    //Optionally trigger addedList change of your enum. This will in turn call GetEntries() again
    protected override IObservable<object> GetEntriesChangedObservable() => _devicesChanged;

    internal Device? GetDeviceBySN(string serialNumber)
    {
        foreach (var netDevice in _netDevices)
            if (netDevice.Value.GetDeviceInfo().SerialNumber() == serialNumber)
                return netDevice.Value;
        return null;
    }

    //Optionally disable alphabetic sorting
    protected override bool AutoSortAlphabetically => false; //true is the default
}