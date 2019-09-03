// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.VisualStudio.Telemetry
{
    public static class TelemetryUtility
    {
        public static string CreateFileAndForgetEventName(string typeName, string memberName)
        {
            if (string.IsNullOrEmpty(typeName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(typeName));
            }

            if (string.IsNullOrEmpty(memberName))
            {
                throw new ArgumentException(Resources.Argument_Cannot_Be_Null_Or_Empty, nameof(memberName));
            }

            return $"{VSTelemetrySession.VSEventNamePrefix}fileandforget/{typeName}/{memberName}";
        }
    }
}
