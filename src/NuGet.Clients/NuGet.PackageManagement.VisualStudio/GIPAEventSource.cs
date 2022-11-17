// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace NuGet.PackageManagement.VisualStudio
{
    public class GIPAEventSource : EventSource
    {
        public static GIPAEventSource Instance { get; private set; } = new GIPAEventSource();

        public void LogCall(string containingType, string methodName, long duration, long frequencyTicksPerSecond)
        {
            base.WriteEvent(1, containingType, methodName, duration, frequencyTicksPerSecond);
        }
    }
}
