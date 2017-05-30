// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace NuGet.Common
{
    public static class LoggingExtensions
    {
        public static string FormatWithCode(this ILogMessage message)
        {
            return $"{message.Code.GetName()}: {message.Message}";
        }

        public static string GetName(this NuGetLogCode code)
        {
            return Enum.GetName(typeof(NuGetLogCode), code);
        }
    }
}
