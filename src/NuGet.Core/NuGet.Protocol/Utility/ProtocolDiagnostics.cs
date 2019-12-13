// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace NuGet.Protocol.Utility
{
    public static class ProtocolDiagnostics
    {
        public delegate void ProtocolDiagnosticEventHandler(ProtocolDiagnosticEvent pdEvent);

        public delegate void ProtocolDiagnosticResourceEventHandler(ProtocolDiagnosticResourceEvent pdrEvent);

        public static event ProtocolDiagnosticEventHandler Event;

        public static event ProtocolDiagnosticResourceEventHandler ResourceEvent;

        internal static void RaiseEvent(ProtocolDiagnosticEvent pdEvent)
        {
            Event?.Invoke(pdEvent);
        }

        internal static void RaiseEvent(ProtocolDiagnosticResourceEvent pdrEvent)
        {
            ResourceEvent?.Invoke(pdrEvent);
        }
    }
}
