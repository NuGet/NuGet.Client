// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using NuGet.Common;

namespace NuGet.VisualStudio.Telemetry
{
    [Export(typeof(INuGetTelemetryProvider))]
    internal class NuGetTelemetryProvider : INuGetTelemetryProvider
    {
        public void EmitEvent(TelemetryEvent telemetryEvent)
        {
            TelemetryActivity.EmitTelemetryEvent(telemetryEvent);
        }

        public async Task PostFaultAsync(Exception e, string callerClassName, [CallerMemberName] string callerMemberName = null, IDictionary<string, object> extraProperties = null)
        {
            await TelemetryUtility.PostFaultAsync(e, callerClassName, callerMemberName, extraProperties);
        }

        public void PostFault(Exception e, string callerClassName, [CallerMemberName] string callerMemberName = null, IDictionary<string, object> extraProperties = null)
        {
            NuGetUIThreadHelper.JoinableTaskFactory.Run(async () =>
            {
                await PostFaultAsync(e, callerClassName, callerMemberName, extraProperties);
            });
        }
    }
}
