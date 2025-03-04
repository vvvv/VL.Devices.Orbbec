﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VL.Core.CompilerServices;

namespace VL.Devices.Orbbec
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="Name"></param>
    /// <param name="DefaultValue"></param>
    public record PropertyInfo(string Name, object CurrentValue, string Description, object Minimum, object Maximum, Spread<string> Entries, string Type, bool IsReadonly, bool IsLocked)
    {
    }
}
