// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

#nullable enable

using NuGet.Common;
using NuGet.PackageManagement.Telemetry;

namespace NuGetVSExtension
{
    internal sealed class CopilotToolSessionResultTelemetryEvent : TelemetryEvent
    {
        internal const string CopilotToolSessionResultEventName = "CopilotToolSessionResult";
        internal const string OriginPropertyName = "Origin";
        internal const string ToolNamePropertyName = "ToolName";
        internal const string ErrorTypePropertyName = "ErrorType";

        internal CopilotToolSessionResultTelemetryEvent(
            NavigationOrigin navigationOrigin,
            string toolName,
            CopilotToolSessionError errorType)
            : base(CopilotToolSessionResultEventName)
        {
            this[OriginPropertyName] = navigationOrigin;
            this[ToolNamePropertyName] = toolName;
            this[ErrorTypePropertyName] = errorType;
        }
    }
}
