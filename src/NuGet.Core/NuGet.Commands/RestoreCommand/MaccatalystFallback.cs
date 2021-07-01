// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;

using NuGet.Client;
using NuGet.Frameworks;

namespace NuGet.Commands
{
    internal class MaccatalystFallback
    {
        internal bool _usedXamarinIOs { get; set; } = false;

        internal static MaccatalystFallback FallbackIfNeeded(NuGetFramework framework)
        {
            if (framework.HasPlatform && framework.Version.Major >= 6 && framework.Platform.Equals("maccatalyst", StringComparison.OrdinalIgnoreCase))
            {
                return new MaccatalystFallback();
            }
            else
            {
                return null;
            }
        }

        internal static void CheckFallback(MaccatalystFallback fallback, IDictionary<string, object> properties)
        {
            if (fallback != null)
            {
                if (properties.TryGetValue(ManagedCodeConventions.PropertyNames.TargetFrameworkMoniker, out object tfmObj))
                {
                    var tfm = (NuGetFramework)tfmObj;
                    if (tfm.Framework.Equals(FrameworkConstants.FrameworkIdentifiers.XamarinIOs, StringComparison.OrdinalIgnoreCase))
                    {
                        fallback._usedXamarinIOs = true;
                    }
                }
            }
        }
    }
}

