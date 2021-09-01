// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.PackageManagement.Telemetry
{
    public class ExternalHyperlinkEvent : TelemetryEvent
    {
        public ExternalHyperlinkType HyperlinkType { get; }

        public ExternalHyperlinkEvent(ExternalHyperlinkType hyperlinkType) : base(nameof(ExternalHyperlinkEvent))
        {
            base[nameof(HyperlinkType)] = hyperlinkType;
        }
    }
}
