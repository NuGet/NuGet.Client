// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Utility
{
    public static class ProtocolDiagnostics
    {
        public delegate void ProtocolDiagnosticEventHandler(ProtocolDiagnosticEvent pdEvent);

        public static event ProtocolDiagnosticEventHandler Event;

        internal static void RaiseEvent(ProtocolDiagnosticEvent pdEvent)
        {
            Event?.Invoke(pdEvent);
        }
    }
}
