// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.SolutionRestoreManager
{
    class RestoreNominationEvent : TelemetryEvent
    {
        public RestoreNominationEvent(
            string eventName,
            string Project
            ) :
            base(eventName, new Dictionary<string, object>
                {
                    { nameof(Project), Project },
                })
        {
        }

        public string Project => (string)base[nameof(Project)];
    }
}
