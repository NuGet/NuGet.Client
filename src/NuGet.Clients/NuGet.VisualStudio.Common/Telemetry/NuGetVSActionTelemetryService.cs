// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using NuGet.Common;
using NuGet.VisualStudio.Telemetry;

namespace NuGet.VisualStudio
{
    /// <summary>
    /// Telemetry service class for restore operation
    /// </summary>
    public class NuGetVSActionTelemetryService : INuGetTelemetryService
    {
        private ITelemetrySession _telemetrySession;

        public string OperationId { get; }

        public NuGetVSActionTelemetryService(ITelemetrySession telemetrySession)
        {
            _telemetrySession = telemetrySession ?? throw new ArgumentNullException(nameof(telemetrySession));
            OperationId = Guid.NewGuid().ToString();
        }

        public NuGetVSActionTelemetryService():
            this(VSTelemetrySession.Instance)
        {
        }

        public void EmitTelemetryEvent(TelemetryEvent telemetryData)
        {
            if (telemetryData == null)
            {
                throw new ArgumentNullException(nameof(telemetryData));
            }

            telemetryData[nameof(OperationId)] = OperationId;

            _telemetrySession.PostEvent(telemetryData);
        }
    }
}
