// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Common
{
    /// <summary>
    /// Interface to post telemetry events.
    /// </summary>
    public interface ITelemetrySession
    {
        void PostEvent(TelemetryEvent telemetryEvent);
    }
}
