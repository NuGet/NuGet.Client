// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace NuGet.Protocol.Core.v3.Data
{
    public static class DataTraceSources
    {
#if !DNXCORE50
        public static readonly TraceSource DataClient = new TraceSource(typeof(DataClient).FullName);

        public static IEnumerable<TraceSource> GetAllSources()
        {
            return typeof(DataTraceSources).GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => typeof(TraceSource).IsAssignableFrom(f.FieldType))
                .Select(f => (TraceSource)f.GetValue(null));
        }
#endif

        internal static void Verbose(string format, params string[] message)
        {
#if !DNXCORE50
            DataClient.TraceEvent(TraceEventType.Verbose, 0, String.Format(CultureInfo.InvariantCulture, format, message));
#endif
        }

        internal static void Verbose(string message)
        {
#if !DNXCORE50
            DataClient.TraceEvent(TraceEventType.Verbose, 0, message);
#endif
        }
    }
}
