using Orbbec;
using VL.Core.Import;
using VL.Devices.Orbbec.Advanced;

[assembly: ImportAsIs(Namespace = "VL")]
[assembly: IncludeForeign]
[assembly: ImportType(typeof(OrbbecDevice), Category = "Devices.Orbbec.Device")]