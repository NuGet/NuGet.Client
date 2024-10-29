// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Events
{
    public sealed class ProtocolDiagnosticServiceIndexEntryEvent
    {
        public string Source { get; }
        public bool HttpsSourceHasHttpResource { get; }

        public ProtocolDiagnosticServiceIndexEntryEvent(string source, bool httpsSourceHasHttpResource)
        {
            Source = source;
            HttpsSourceHasHttpResource = httpsSourceHasHttpResource;
        }
    }
}
