// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.Internal.VisualStudio.Diagnostics;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Telemetry service class for restore operation
    /// </summary>
    public class NuGetVSTelemetryService : INuGetTelemetryService
    {
        private const string VSCodeMarkerPrefix = "VS_Nuget_";

        private ITelemetrySession _telemetrySession;

        public NuGetVSTelemetryService(ITelemetrySession telemetrySession)
        {
            _telemetrySession = telemetrySession ?? throw new ArgumentNullException(nameof(telemetrySession));
        }

        public NuGetVSTelemetryService():
            this(VSTelemetrySession.Instance)
        {
        }

        public virtual void EmitTelemetryEvent(TelemetryEvent telemetryData)
        {
            if (telemetryData == null)
            {
                throw new ArgumentNullException(nameof(telemetryData));
            }

            _telemetrySession.PostEvent(telemetryData);
        }

        public virtual void EmitTelemetryMarker(string telemetryMarkerName)
        {
            if (telemetryMarkerName == null)
            {
                throw new ArgumentNullException(nameof(telemetryMarkerName));
            }

            if (VsEtwLogging.IsProviderEnabled(VsEtwKeywords.Ide, VsEtwLevel.Information))
            {
                VsEtwLogging.WriteEvent(VSCodeMarkerPrefix + telemetryMarkerName, VsEtwKeywords.Ide, VsEtwLevel.Information, new { startupType = 2, instanceSuffix = "None" });
            }
        }
    }
}
