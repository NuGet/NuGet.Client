// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics.Tracing;

namespace NuGet.Common
{
    [EventSource(Name = "NuGetETWProvider")]
    internal sealed class NuGetETWProvider : EventSource
    {
        private static Lazy<NuGetETWProvider> LazyInstance = new Lazy<NuGetETWProvider>(() => new NuGetETWProvider());

        private NuGetETWProvider()
        {
        }

        internal static NuGetETWProvider Instance
        {
            get
            {
                return LazyInstance.Value;
            }
        }

        internal void WriteEventData(string name, string eventJsonString)
        {
            if (IsEnabled())
            {
                WriteEvent(1, name, eventJsonString);
            }
        }
    }
}
