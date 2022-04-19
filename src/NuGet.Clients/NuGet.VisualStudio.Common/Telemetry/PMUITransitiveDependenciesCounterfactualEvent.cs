// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio
{
    public class PMUITransitiveDependenciesCounterfactualEvent : TelemetryEvent
    {
        internal const string EventName = "PMUITransitiveDependenciesCounterfactual";

        public PMUITransitiveDependenciesCounterfactualEvent() : base(EventName)
        {
        }
    }
}
