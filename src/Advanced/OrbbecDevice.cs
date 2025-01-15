using System.Reactive.Linq;
using VL.Core.CompilerServices;
using Orbbec;
using System.Reactive.Subjects;
using VL.Lib.Basics.Resources;

namespace VL.Devices.Orbbec.Advanced;

internal record DeviceInfo(uint Index, string SerialNumber);

[Serializable]
public class OrbbecDevice : DynamicEnumBase<OrbbecDevice, OrbbecDeviceDefinition>
{
    public OrbbecDevice(string value) : base(value)
    {
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
    private Device _netDevice;

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

    public void SetNetDevice(Device device)
    {
        _netDevice = device;
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
            { "Default", _netDevice != null ? new DeviceInfo(0, _netDevice.GetDeviceInfo().SerialNumber()) : devices.DeviceCount() > 0 ? new DeviceInfo(0, devices.SerialNumber(0)) : null }
            //{ "Default", devices.DeviceCount() > 0 ? devices.GetDevice(0) : null }
        };

        uint iOff = 0;
        if (_netDevice != null)
        {
            var dvcInfo = _netDevice.GetDeviceInfo();
            result.Add(dvcInfo.Name() + " " + dvcInfo.ConnectionType(), new DeviceInfo(0, dvcInfo.SerialNumber()));
            iOff++;
        }

        for (uint i = 0; i < devices.DeviceCount(); i++)
        {
            var name = devices.Name(i) + " - " + devices.SerialNumber(i);
            if (!result.ContainsKey(name))
            {
                result.Add(name, new DeviceInfo(i+iOff, devices.SerialNumber(i)));
            }
        }

        return result!;
    }

    //Optionally trigger addedList change of your enum. This will in turn call GetEntries() again
    protected override IObservable<object> GetEntriesChangedObservable() => _devicesChanged;

    //Optionally disable alphabetic sorting
    protected override bool AutoSortAlphabetically => false; //true is the default
}