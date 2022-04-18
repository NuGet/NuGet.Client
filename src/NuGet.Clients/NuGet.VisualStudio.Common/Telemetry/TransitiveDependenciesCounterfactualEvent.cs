// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using NuGet.Common;

namespace NuGet.VisualStudio
{
    public class TransitiveDependenciesCounterfactualEvent : TelemetryEvent
    {
        internal const string EventName = "TransitiveDependenciesCounterfactual";

        public TransitiveDependenciesCounterfactualEvent() : base(EventName)
        {
        }
    }
}
